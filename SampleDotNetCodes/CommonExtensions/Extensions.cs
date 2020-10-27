using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Common.Extensions
{
    public static class Extensions
    {
        
        public static int ToIntegerSSNAsPrimaryKey(this string ssnString)
        {
            int intSSN = 0; 
            if (!String.IsNullOrEmpty((ssnString ?? "").Trim()))
            {
                ssnString = Regex.Replace(ssnString, @"[^0-9]+", string.Empty); // remove non numeric
                bool result = Int32.TryParse(ssnString, out intSSN);
                if (result == false)
                {
                    intSSN = 0;
                }
            }
             return intSSN;
        }

        public static string ConvertToStandardDate(this string dateStr, string dateStrFormat)
        {
            try
            {
                if (dateStr.Length > 0)
                {
                    dateStrFormat = dateStrFormat.Trim().ToLower();
                    int yPos = dateStrFormat.IndexOf('y');
                    int mPos = dateStrFormat.IndexOf('m');
                    int dPos = dateStrFormat.IndexOf('d');
                    string[] parts;

                    if (yPos == 0)
                    { // date start with year
                        dateStr = mPos < dPos ? dateStr.Insert(mPos, "/") : dateStr.Insert(dPos, "/");
                        dateStr = mPos < dPos ? dateStr.Insert(dPos + 1, "/") : dateStr.Insert(mPos + 1, "/");

                        // move year to the end of date string
                        parts = dateStr.Split('/');
                        dateStr = dateStr.Insert(dateStr.Length, "/" + parts[0]).Remove(0, dateStr.IndexOf("/") + 1);
                    }
                    else
                    { // date end with year
                        dateStr = mPos < dPos ? dateStr.Insert(dPos, "/") : dateStr.Insert(mPos, "/");
                        dateStr = dateStr.Insert(yPos + 1, "/");
                    }

                    // date string must start with month
                    if (mPos > dPos)
                    {
                        parts = dateStr.Split('/');
                        dateStr = parts[1] + "/" + parts[0] + "/" + parts[2];
                    }

                    dateStr = String.Format("{0:MM/dd/yy}", Convert.ToDateTime(dateStr));
                }

            }
            catch (Exception)
            {
                dateStr = string.Empty;
            }

            return dateStr;
        }

        public static string MatchDateFormat(this String fileName)
        {
            List<Object> maps = new List<Object>();
            maps.Add(new { reg = new Regex(@"[.][A]\d{2}\d{2}\d{2}[.][A]"), format = ".AYYMMDD.A" });
            maps.Add(new { reg = new Regex(@"\d{8}"), format = "YYYYMMDD" });
            maps.Add(new { reg = new Regex(@"[A-Z]{3}\d{2}\d{2}"), format = "MONDDYY" });
            maps.Add(new { reg = new Regex(@"[A-Z]{3}\d{4}"), format = "MONYYYY" });
            maps.Add(new { reg = new Regex(@"\d{2}\d{2}\d{2}\.T\d{2}"), format = "YYMMDD.TYY" });
            maps.Add(new { reg = new Regex(@"\d{2}\d{2}\d{2}"), format = "MMDDYY" });
            maps.Add(new { reg = new Regex(@"\d{2}\.\d{2}\.\d{4}"), format = "MM.DD.YYYY" });

            string dateFormat = maps
                .Where(m => ((Regex)m.GetType().GetProperty("reg").GetValue(m)).IsMatch(fileName))
                .Select(m => m.GetType().GetProperty("format").GetValue(m).ToString())
                .FirstOrDefault();
            return  fileName.Substring(0,4).ToUpper() =="LTC_" ? "YYYYMMDD" : dateFormat;
        }

        public static void MapChangedValues(this object entity, object model)
        {
            var modelProperties = TypeDescriptor.GetProperties(model).Cast<PropertyDescriptor>();
            var entityProperties = TypeDescriptor.GetProperties(entity).Cast<PropertyDescriptor>();

            foreach (var ep in entityProperties)
            {
                if (!ep.Name.StartsWith("Created") && !ep.Name.StartsWith("Modified"))
                {
                    if ( ep.PropertyType.IsValueType || ep.PropertyType == typeof(string) )
                    {
                        var modelProperty = modelProperties.FirstOrDefault(p => p.Name == ep.Name);
                        bool isRequired = Attribute.IsDefined(ep.ComponentType.GetProperty(ep.Name), typeof(RequiredAttribute));
                        bool isKey = Attribute.IsDefined(ep.ComponentType.GetProperty(ep.Name), typeof(KeyAttribute));

                        if (modelProperty != null )
                        {
                            // No null string ia allowed for key fields, use empty string instead
                            var modelValue = modelProperty.GetValue(model);
                            modelValue = isKey && modelProperty.PropertyType == typeof(string) && modelValue == null ? string.Empty : modelValue; 

                            ep.SetValue(entity, modelValue);

                            if (ep.PropertyType == typeof(string) && !isRequired && (string)ep.GetValue(entity) == string.Empty)
                            {
                                ep.SetValue(entity, null);  // set to null to avoid foreign key violation for null allowed column 
                            }
                        }
                    }
                    else
                    {
                        ep.SetValue(entity, null);
                    }
                }
            }
        }
 
        public static bool AnyPropertyValueChanged(this object entity, object model)
        {
            bool valueChanged = false;

            var modelProperties = TypeDescriptor.GetProperties(model).Cast<PropertyDescriptor>();
            var entityProperties = TypeDescriptor.GetProperties(entity).Cast<PropertyDescriptor>();

            foreach (var ep in entityProperties)
            {
                if (!ep.Name.StartsWith("Created") && !ep.Name.StartsWith("Modified"))
                {
                    if (ep.PropertyType.IsValueType || ep.PropertyType == typeof(string))
                    {
                        var modelProperty = modelProperties.FirstOrDefault(p => p.Name == ep.Name);

                        if (modelProperty != null)
                        {
                            var oldValue = ep.GetValue(entity);
                            var newValue = modelProperty.GetValue(model);

                            if (!Object.Equals(oldValue, newValue))
                            {
                                valueChanged = true;
                            }
                        }
                    };
                }
            }

            return valueChanged;
        }


        public static string ChangedPropertyXmlNodes(this object entity, object model)
        {
            StringBuilder xmlNodesStr = new StringBuilder("");

            var modelProperties = TypeDescriptor.GetProperties(model).Cast<PropertyDescriptor>();
            var entityProperties = TypeDescriptor.GetProperties(entity).Cast<PropertyDescriptor>();

            foreach (var ep in entityProperties)
            {
                if (!ep.Name.StartsWith("Created") && !ep.Name.StartsWith("Modified"))
                {
                    if (ep.PropertyType.IsValueType || ep.PropertyType == typeof(string))
                    {
                        var modelProperty = modelProperties.FirstOrDefault(p => p.Name == ep.Name);

                        var oldValue = ep.GetValue(entity) ?? (ep.PropertyType == typeof(string) ? string.Empty : null);
                        var newValue = ep.GetValue(model) ?? (ep.PropertyType == typeof(string) ? string.Empty : null);

                        if (modelProperty != null && !Object.Equals(oldValue, newValue))
                        {
                            xmlNodesStr.Append(
                                "<Field " +
                                "name='" + modelProperty.Name + "' " +
                                "old-value='" + (oldValue != null ? oldValue.ToString() : string.Empty) + "' " +
                                "new-value='" + (newValue != null ? newValue.ToString() : string.Empty) + "' " +
                                "/>"); 
                        }
                    };
                }
            }

            return xmlNodesStr.ToString();
        }

        public static string GetEmployeeIDJobSeq(this string employeeID)
        {

            string jobSequence = "1";

            if (!String.IsNullOrEmpty(employeeID))
            {

                jobSequence = employeeID.Trim().Length == 8 ? employeeID.Trim().Substring(7, 1) : "1";

            }

            return jobSequence;
        }

        public static string LeftAlignZipCode(this string zip)
        {
            zip = string.IsNullOrEmpty(zip.Trim()) ? "00000" : zip.Trim();
            zip = zip.Replace("-", "0");
            zip = zip.Length < 5 ? zip.PadRight(5, ' ') : zip.Substring(0, 5);
            zip = zip == "00001" ? "10001" : zip;
            return zip;
        }

           

    }
}
