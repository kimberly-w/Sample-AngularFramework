import { Component, OnInit, Input, Output, EventEmitter } from '@angular/core';
import { Fund } from 'src/app/models/fund';
import { ApiService } from 'src/app/services/api.service';
@Component({
  selector: 'app-fund',
  templateUrl: './fund.component.html',
  styleUrls: ['./fund.component.css']
})
export class FundComponent implements OnInit {
  funds: Fund[] = [
    {id: 4200, name: 'AMZON holding Coop', symbol: 'AMZON', price: 2950, manager: 'Large Cap management'},
    {id: 5340, name: 'TESLA Mob Crop', symbol: 'TSLA', price: 425, manager: ''},
    {id: 1976, name: 'iApple Mobile Inc', symbol: 'IAPP', price: 195, manager: ''},
  ];
  addedCompanyName: string;

  @Input() creationObject: string;
  @Output() addedData: EventEmitter<string> = new EventEmitter();

  updateEmployeeName: string;
  admin = true;
  dataSubject: any = {};
  constructor(private dataService: ApiService) {}
  ngOnInit(): void {
        // Components havn't had a relateship
        this.dataService.currentData.subscribe((response) => {
             this.dataSubject = response;
        });
  }
  // This event is declared inside Child component
  addNewData() {
    this.addedData.emit(this.creationObject);
  }
  // This is a Callback handler and wait for the updateChild call back
  updateEmployeeHandler(newData: string) {

  }
}
