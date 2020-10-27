import { Component, OnInit, EventEmitter, ComponentFactoryResolver, OnDestroy, NgZone,
  ViewChild, ViewChildren, ViewContainerRef, Input, Output, QueryList  } from '@angular/core';
import { ApiService } from 'src/app/services/api.service';
import { ReactiveFormsModule, FormsModule, FormControl, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

// import { CreateComponent } from 'src/app/components/create/create.component';
import { FundComponent } from 'src/app/components/fund/fund.component';
import * as _ from 'lodash';
import { Product } from 'src/app/models/product';
import { Employee } from 'src/app/models/employee';
import { Router } from '@angular/router';
import { TokenizeResult } from '@angular/compiler/src/ml_parser/lexer';
import { Fund } from 'src/app/models/fund';

const groupKey = (list: any[], keyGetter: any): any[] =>  {
  return list.reduce((acc, currentItem) => {
         (acc[currentItem[keyGetter]] = acc[currentItem[keyGetter]] || []).push(currentItem);
         return acc;
  });
};

// function reduce<TElement, TResult>{
//   array: TElement[],
//   reduce: (result: TResult, el: TElement) => TokenizeResult,
//   initialResult: TokenizeResult): TResult {
//     let result = initialResult;
//     for (const element of array) {
//        result = reducer(result, element);
//     }
//     return result;
//   }
// }
// const ageByName = reduce<Employee, Record<string, number>>(
//   persons,
//   (result, person) => ({
//       ...result,
//       [person.name]: person.age
//   }),
//   {}
// );
@Component({
  selector: 'app-add',
  templateUrl: './add.component.html',
  styleUrls: ['./add.component.css']
})
export class AddComponent implements OnInit {
  employeeForm: FormGroup;
// products: Product[] = new Array<Product>();
  submitted = false;
  product: Product;
  states: any = ['New Jersey', 'New York', 'Washington', 'California', 'Floride'];
  cities: any = ['Albany', 'New York', 'Manhattan', 'Buffalo'];
  employee: Employee = new Employee();
  EmployeeProfile: any = ['iApple mobile', 'Samsung Galaxy s10', 'verrized mobile'];

  companies: string[] = new Array<string>();
  admin = false;
  addCompany: string;
  phoneNbr: number;
  employeeMap: Map<number, Employee[]> = new Map<number, Employee[]>();

  // creatModel: CreateComponent;
  @ViewChild(FundComponent, {static: true}) createComponentRef: FundComponent;
  // @ViewChildren(CreateComponent) createEmployeesSectors: QueryList<any>;

  constructor(public fb: FormBuilder, private apiService: ApiService, public router: Router) {
    // Validators
    this.employeeForm = this.fb.group({
      name: ['', [Validators.required]],
      // email: ['', [Validators.required, Validators.pattern('[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,3}$')]],
      email: ['', [Validators.required]],
      designation: ['', [Validators.required]],
      phoneNumber: ['', [Validators.required, Validators.pattern('^[0-9]+$')]]
    });

    this.admin = !this.admin;
    this.addCompany = 'Tiffany Cop!!!';
  }

  ngOnInit(): void {
    // this.employeeMap.set(0, this.employee[0]);
  }

  changeCity(event) {
    this.employeeForm.get('cityName').setValue( event.target.value, { onlySelf: true });
  }

  changeState(event) {
    this.employeeForm.get('stateName').setValue( event.target.value, { onlySelf: true });
  }

  // createProduct() {
  appendBenefits() {
    this.apiService.createEmployee(this.employee).subscribe((response: Employee) => {
      // populate Employee Form
      // this.employeeForm.id = response.id;
      // let responseMsg = response !== null ? response : null;
      this.employeeForm.setValue(response);
      this.employeeForm.value.phoneNumber = 'update phone number';
      this.submitted = true;
    });
  }

  newProduct() {}

  updateProfile(event) {}

  onSubmit() {
     // temporary: remove commits after test
     this.apiService.createEmployee(this.employee).subscribe((response: Employee) => {
          this.employeeForm.setValue(response);
          // this.apiService.changeDataSub(response);
          // this.router.navigate(['add']);
      });
  }

  addDataHandler(newEvent: string) {
      this.companies.push(newEvent);
  }

  onRegister(event: MouseEvent) {
     event.stopPropagation();
  }

  get getEmployeeForm() {
    return this.employeeForm.controls;
  }

  OnDestroy() {
  }
}
