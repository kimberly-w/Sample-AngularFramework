import { Component } from '@angular/core';
import { Employee } from 'src/app/models/Employee';
import { ApiService } from 'src/app/services/api.service';
@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  title = 'Angular Demo App';
  // public employees: Employee[];

  constructor(private apiService: ApiService) {}
  ngOninti() {
     // this.employees = this.apiService.employees;
  }
}
