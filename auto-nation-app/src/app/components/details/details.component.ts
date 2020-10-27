import { Component, OnInit, Input, Output, NgZone } from '@angular/core';
import { ActivatedRoute, Params, Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { ApiService } from 'src/app/services/api.service';
import { Employee } from 'src/app/models/Employee';
@Component({
  selector: 'app-details',
  templateUrl: './details.component.html',
  styleUrls: ['./details.component.css']
})
export class DetailsComponent implements OnInit {
@Input() employee: Employee;
data: any = new Employee();

id: number;
name: string;
email: string;
mobileNbr: string;
submitted = false;
employeeDetailsForm: FormGroup;

  // tslint:disable-next-line: max-line-length
  constructor(private route: ActivatedRoute, public fb: FormBuilder, public apiService: ApiService, public ngZone: NgZone, public router: Router) {
    // this.route.params.subscribe(params => console.log(params));
      this.employeeDetailsForm = this.fb.group({
        // name: ['', [Validators.required]],
        email: ['', [Validators.required, Validators.pattern('[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,3}$')]],
        // email: ['', [Validators.required]],
        mobileNbr: ['', [Validators.required, Validators.pattern('^[0-9]+$')]]
      });
  }

  ngOnInit(): void {
    this.route.queryParams.subscribe((params: Params) => {
         // this.id = params['id'];
         this.name = params['name'];
         this.mobileNbr = params['phone'];
         this.email = params['email'];
    });
  }

  onSubmit() {
    // this.apiService.createProduct(this.product).subscribe( resolvedProduc => {});
  }

  onUpdateEmployeeDetails(updateEmployee) {
      if (updateEmployee !== null) {
          this.apiService.updateEmployeeDetail(updateEmployee).subscribe( response => {
            if (response !== null) {
                // Populate Successed updated employee mobile nbr
                // this.router.navigate(['list']);
            }
          });
      }
      this.router.navigate(['list']);
  }

  onUpdateClose() {
      this.onUpdateEmployeeDetails(null);
  }

 get getEmployeeDetailForm() {
     return this.employeeDetailsForm.controls;
 }
}
