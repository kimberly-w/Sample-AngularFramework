import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { AddComponent } from './components/add/add.component';
import { ListComponent } from './components/list/list.component';
import { DetailsComponent } from './components/details/details.component';

const routes: Routes = [
  { path: '', redirectTo: 'list', pathMatch: 'full'},
  { path: 'add', component: AddComponent },
  { path: 'list', component: ListComponent },
  { path: 'details', component: DetailsComponent }
  // { path: 'details/:id', component: DetailsComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
