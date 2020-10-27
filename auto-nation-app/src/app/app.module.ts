import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { HttpClientModule } from '@angular/common/http';
import { ReactiveFormsModule, FormsModule, FormControl, FormBuilder, FormGroup, Validators } from '@angular/forms';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';

import { AddComponent } from './components/add/add.component';
import { DetailsComponent } from './components/details/details.component';
import { ListComponent } from './components/list/list.component';
import { ApiService } from './services/api.service';
import { PhonePipe } from './pipes/phone.pipe';
import { OrderByPipe } from './pipes/order-by.pipe';
import { SortByColumnPipe } from './pipes/sort-by-column.pipe';
import { FundComponent } from './components/fund/fund.component';
// import { CreateComponent } from './components/create/create.component';
// import { UpdateComponent } from './components/update/update.component';

@NgModule({
  declarations: [
    AppComponent,
    AddComponent,
    DetailsComponent,
    ListComponent,
    PhonePipe,
    OrderByPipe,
    SortByColumnPipe,
    FundComponent,
   // CreateComponent,
   // UpdateComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    ReactiveFormsModule,
    FormsModule,
    HttpClientModule
  ],
  providers: [ ApiService ],
  bootstrap: [AppComponent],
  entryComponents: []
})
export class AppModule { }
