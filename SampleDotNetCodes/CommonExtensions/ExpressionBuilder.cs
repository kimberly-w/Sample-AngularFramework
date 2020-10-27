using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Common.Models;

namespace Common.Extensions
{
    public static class ExpressionBuilder
    {
        // makes expression for specific prop
        public static Expression<Func<T, object>> GetOrderByExpression<T>(string orderByProperty)
        {
            var param = Expression.Parameter(typeof(T), "x");
            Expression conversion = Expression.Convert(Expression.Property(param, orderByProperty), typeof(object));

            return Expression.Lambda<Func<T, object>>(conversion, param);
        }

        // makes deleget for specific prop
        public static Func<TSource, object> GetOrderByFunc<TSource>(string propertyName)
        {
            return GetOrderByExpression<TSource>(propertyName).Compile();  //only need to compiled expression
        }

        //OrderBy overloads
        public static IOrderedQueryable<TSource> OrderBy<TSource>(this IQueryable<TSource> source, string propertyName)
        {
            return source.OrderBy(GetOrderByExpression<TSource>(propertyName));
        }
        public static IOrderedEnumerable<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, string propertyName)
        {
            return source.OrderBy(GetOrderByFunc<TSource>(propertyName));
        }

        //OrderByDescending overloads
        public static IOrderedQueryable<TSource> OrderByDescending<TSource>(this IQueryable<TSource> source, string propertyName)
        {
            return source.OrderByDescending(GetOrderByExpression<TSource>(propertyName));
        }
        public static IOrderedEnumerable<TSource> OrderByDescending<TSource>(this IEnumerable<TSource> source, string propertyName)
        {
            return source.OrderByDescending(GetOrderByFunc<TSource>(propertyName));
        }


        public static Expression<Func<T, bool>> GetFilterExpression<T>(IList<Filter> filters)
        {
            if (filters == null || filters.Count == 0)
                return null;

            ParameterExpression param = Expression.Parameter(typeof(T), "t");
            Expression exp = null;

            foreach (Filter filter in filters)
            {
                exp = exp == null ? GetFilterExpression<T>(param, filter) : Expression.AndAlso(exp, GetFilterExpression<T>(param, filter));
            }

            return Expression.Lambda<Func<T, bool>>(exp, param);
        }

        private static Expression GetFilterExpression<T>(ParameterExpression param, Filter filter)
        {
 
            MemberExpression member = null;
            Type underlyingType = null;
            bool isNullable = false;

            if (filter.field.Contains('.'))
            {
                var props = filter.field.Split('.');
                var type = typeof(T);
                Expression expr = param;
                foreach (var prop in props)
                {
                    var camelName = char.ToUpper(prop[0]) + prop.Substring(1);
                    var pi = type.GetProperty(camelName) ?? type.GetProperty(prop);
                    expr = pi != null ? Expression.Property(expr, pi) : null;
                    type = pi != null ? pi.PropertyType : null;
                    
                }
                member = (MemberExpression)(expr ?? param);
                isNullable = IsNullableType(member.Member.ReflectedType, member.Member.Name, out underlyingType);
            }
            else
            {
                member = Expression.Property(param, filter.field);
                // Convert nullable type to type acceptable to expression tree
                isNullable = IsNullableType(typeof(T), member.Member.Name, out underlyingType);
            }           
           
            TypeCode typeCode = underlyingType != null ? Type.GetTypeCode(underlyingType) : TypeCode.Empty;
            
            List<object> args = new List<object>();
            ConstantExpression constant;
            switch (typeCode)
            {
                case TypeCode.Int16:
                    constant = Expression.Constant(Convert.ToInt16(filter.value ?? 0), isNullable ? typeof(short?) : typeof(short));
                    break;
                case TypeCode.Int32:
                    constant = Expression.Constant(Convert.ToInt32(filter.value ?? 0), isNullable ? typeof(int?) : typeof(int));
                    break;
                case TypeCode.Int64:
                    constant = Expression.Constant(Convert.ToInt64(filter.value ?? 0), isNullable ? typeof(long?) : typeof(long));
                    break;
                case TypeCode.Decimal:
                    constant = Expression.Constant(Convert.ToDecimal(filter.value ?? 0), isNullable ? typeof(decimal?) : typeof(decimal));
                    break;
                case TypeCode.Double:
                    constant = Expression.Constant(Convert.ToDouble(filter.value ?? 0), isNullable ? typeof(double?) : typeof(double?));
                    break;
                case TypeCode.Boolean:
                    constant = Expression.Constant(Convert.ToBoolean(filter.value), isNullable ? typeof(bool?) : typeof(bool));
                    break;
                case TypeCode.DateTime:
                    DateTime dt;
                    if ( DateTime.TryParse(filter.value.ToString(), out dt) )
                    {
                        constant = Expression.Constant(Convert.ToDateTime(filter.value), isNullable ? typeof(DateTime?) : typeof(DateTime));
                    }
                    else
                    {
                        constant = null;
                    }
                    break;
                case TypeCode.String:
                    args.Add(filter.value);
                    args.Add(StringComparison.OrdinalIgnoreCase);
                    constant = null;
                    break;
                default:
                    constant = Expression.Constant(filter.value);
                    break;
            }

            // Following 4 lines code are a simple solution for Angular ui-grid filtering 
            if (typeCode == TypeCode.String)
                filter.@operator = "startswith";
            else
                filter.@operator = "eq";


            // This switch will be emoved when Kendo grid is obsolete from Banafits Manager 
            if (constant != null || args.Count > 0)
            {
                switch (filter.@operator)
                {
                    case "eq":
                        return constant != null ? Expression.Equal(member, constant) : null;

                    case "neq":
                        return constant != null ? Expression.NotEqual(member, constant) : null;

                    case "gt":
                        return constant != null ? Expression.GreaterThan(member, constant) : null;

                    case "gte":
                        return constant != null ? Expression.GreaterThanOrEqual(member, constant) : null;

                    case "lt":
                        return constant != null ? Expression.LessThan(member, constant) : null;

                    case "lte":
                        return constant != null ? Expression.LessThanOrEqual(member, constant) : null;

                    case "contains":
                        return constant != null ?  Expression.Call(member, typeof(string).GetMethod("Contains"), constant) : null;

                    case "startswith":
                        if (args.Count > 0)
                        {
                            var ignoreCaseConstant = args.Select(a => Expression.Constant(a, a.GetType()));
                            var method = typeof(string).GetMethod("StartsWith", args.Select(a => a.GetType()).ToArray());

                            return Expression.Call(member, method, ignoreCaseConstant);
                        }
                        else
                        {
                            return constant != null ? Expression.Call(member, typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), constant) : null;
                        }
                    
                    case "endswith":
                        if (args.Count > 0)
                        {
                            var ignoreCaseConstant = args.Select(a => Expression.Constant(a, a.GetType()));
                            var method = typeof(string).GetMethod("EndsWith", args.Select(a => a.GetType()).ToArray());

                            return Expression.Call(member, method, ignoreCaseConstant);
                        }
                        else
                        {
                            return constant != null ? Expression.Call(member, typeof(string).GetMethod("EndsWith", new[] { typeof(string) }), constant) : null;
                        }
                }
            }

            return null;
        }

        private static bool IsNullableType(Type type, string property, out Type underlyingType)
        {
            //Type type = typeof(T);

            PropertyInfo propertyInfo = type.GetProperty(property);

            bool isNullable = false;
            underlyingType = null;
            if (propertyInfo != null )
            {
                underlyingType = propertyInfo.PropertyType;
                if (propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    isNullable = true;
                    underlyingType = Nullable.GetUnderlyingType(propertyInfo.PropertyType);
                }
            }

            return isNullable;
        }
    }

}
