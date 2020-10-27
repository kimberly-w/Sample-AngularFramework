import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, Observer, Subject, throwError, BehaviorSubject, from } from 'rxjs';
import { filter, map, mergeMap, reduce, catchError, groupBy, toArray } from 'rxjs/operators';
import { Product } from '../models/product';
import { Employee } from 'src/app/models/employee';
import { NgZone } from '@angular/core';
@Injectable({
  providedIn: 'root'
})
export class ApiService {

  public responseSource: Subject<any> = new Subject<any>();
  public response = this.responseSource.asObservable(); // Use this to get the response of value

  data: object = {};
  private updateAddress = new BehaviorSubject<object>( this.data );
  currentData = this.updateAddress.asObservable();

  employees: Employee[] = [
    // tslint:disable-next-line: max-line-length
    {id: 901, name: 'Kathy', email: '@gmail.com', phoneNumber: 8482131499 , designation: 'IT', status: true, hiredDate: '01/01/2020', salary: 95.00 },
      // tslint:disable-next-line: max-line-length
    {id: 765, name : 'David', email: 'gmail.com', phoneNumber: 9091234567 , designation: 'IT', status: true, hiredDate: '01/01/2020', salary: 10.00 },
      // tslint:disable-next-line: max-line-length
    {id: 564, name : 'Ethen', email: '@gmail.com', phoneNumber: 2125479910, designation: 'IT', status: true, hiredDate: '7/31/2017', salary: 90.00 },
      // tslint:disable-next-line: max-line-length
    {id: 834, name : 'Frank', email: '@gmail.com', phoneNumber: 64621390990 , designation: 'Bio', status: true, hiredDate: '8/15/2010', salary: 15.25},
      // tslint:disable-next-line: max-line-length
    {id: 1003, name : 'Roy', email: '@gmail.com', phoneNumber: 3036752344, designation: 'PM', status: false, hiredDate: '01/01/2020', salary: 23.45 },
// tslint:disable-next-line: max-line-length
    {id: 7764, name: 'Joyce', email: '@gmail.com', phoneNumber: 2129085512 , designation: 'Finance', status: true, hiredDate: '01/01/2010', salary: 150.20 },
// tslint:disable-next-line: max-line-length
    {id: 4566, name: 'Amy', email: '@gmail.com', phoneNumber: 2123470922, designation: 'Finance', status: false, hiredDate: '01/01/2010', salary: 710.23 },
// tslint:disable-next-line: max-line-length
    {id: 4610, name: 'Amy', email: '@gmail.com', phoneNumber: 6464684080, designation: 'Account', status: false, hiredDate: '01/01/2010', salary: 650.50 },
// tslint:disable-next-line: max-line-length
    {id: 1158, name: 'Lori', email: '@gmail.com', phoneNumber: 2129076610, designation: 'HR', status: null, hiredDate: '09/01/2015', salary: 12.56 }
  ];

  products: any[] = [
    {id: 9043534, desc: 'Mikes', category: '1', price: 10.25, expiredDate: '12/31/2020'},
    {id: 8890823, desc: 'Choloice', category: '2', price: 24.90, expiredDate: '12/31/2020'},
    {id: 7856128, desc: 'Vegtable', category: '7', price: 76.25, expiredDate: '5/7/2020'},
    {id: 4344876, desc: 'Orange Juice', category: '1', price: 12.50, expiredDate: '12/31/2020'},
    {id: 545460,  desc: 'Organ Bread', category: '7', price: 5.15, expiredDate: '5/7/2020'},
    {id: 453712,  desc: 'Sweets cake', category: '2', price: 70.50, expiredDate: '5/9/2020'},
    {id: 601321,  desc: 'Brown Surger', category: '1', price: 3.55, expiredDate: '5/9/2020'},
    {id: 673466,  desc: 'Jeff Waffer', category: '2', price: 15.50, expiredDate: '5/15/2020'},
    {id: 564551,  desc: 'Apple juice', category: '2', price: 12.50, expiredDate: '6/5/2020'},
    {id: 783453,  desc: 'Gaol fisher cakes', category: '2', price: 12.50, expiredDate: '6/5/2020'},
    {id: 351099,  desc: 'Honey butter', category: '1', price: 3.50, expiredDate: '12/31/2020'}
  ]; //.forEach(product => this.AddProduct(product));

  public endPoint = 'http://localhost:3000/employees';  // URL end pointer Json-server
  httpOptions = {
    headers: new HttpHeaders({
      'Content-Type': 'application/json'
    })
  }

  constructor(private httpClient: HttpClient, public ngZone: NgZone) {}

  // boardcast subject - call handler
  changeDataSub(newData: any) {
     this.updateAddress.next(newData); // A listeren
  }

  createProduct(product): Observable<Product> {
      return this.httpClient.post<Product>(this.endPoint, JSON.stringify(product), this.httpOptions)
      .pipe(
        // flat, flter and catch error
      );
  }

  createEmployee(employee): Observable<Employee> {
       return this.httpClient.post<Employee>(this.endPoint, JSON.stringify(employee), this.httpOptions)
       .pipe(
         catchError( this.errorHandler)
       );
  }

  updateProduct(id, product) {
        return this.httpClient.put( `${this.endPoint}/${id}`, product);
  }

  getAll(): Observable<Product[]> {
    const data = this.httpClient.get<Product[]>(this.endPoint)
    from(data).pipe(
         groupBy((val) => val.category), // Group by category them return an array
         mergeMap(group => {
             return group.pipe(toArray()); }),
         mergeMap((array) => {           // Take each from above then group and returned an array
             return from(array).pipe( groupBy(
             (group) => group.expiredDate,
             ),
             mergeMap(group2 => {
                return group2.pipe(toArray());
             })
         );
      }),
      mergeMap((array) => {
        return from(array).pipe(
          reduce((acc, curr) => ({
            category: curr.category,
            expiredDate: curr.expiredDate,
            price: curr.price + acc.price
          }), { category: '', expiredDate: '', price: 0})
        );
      }),
    ).subscribe(
      //product => console.log(product)
      );
    return data;
  }

  getAllEmployees(): Observable<Employee> {
       return this.httpClient.get<Employee>(this.endPoint);
  }

  updateEmployeeDetail(data) {
     return this.httpClient.put(`${this.endPoint}/${data.id}`, data);
  }

  deleteEmployee(employee) {
     return this.httpClient.delete(`${this.endPoint}/${employee}`);
  }

  getById(id): Observable<Product>{
    return this.httpClient.get<Product>( this.endPoint + id);
  }
  errorHandler(error) {
    let errorMessage = '';
    if (error.error instanceof ErrorEvent) { errorMessage = error.error.message;
    } else {
      errorMessage = `Error Code: ${error.status}\nMessage: ${error.message}`;
    }
    console.log(errorMessage);
    return throwError(errorMessage);
  }
}
