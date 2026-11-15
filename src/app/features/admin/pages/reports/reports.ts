import { Component, inject, OnInit } from '@angular/core';
import { AdminApiService } from '../../services/admin-api.service';
@Component({selector:'app-admin-reports',standalone:true,templateUrl:'./reports.html',styleUrl:'./reports.scss'})
export class AdminReports implements OnInit {
  private readonly api=inject(AdminApiService);
  stats=[{label:'Gross sales',value:'$0.00',change:'Live API'},{label:'Orders processed',value:'0',change:'Live API'},{label:'Average order value',value:'$0.00',change:'Live API'},{label:'Cancelled orders',value:'0',change:'Live API'}];
  months:{month:string;width:number}[]=[];
  ngOnInit():void{this.api.reports().subscribe(x=>{this.stats=[{label:'Gross sales',value:`$${Number(x.grossSales).toFixed(2)}`,change:'Selected period'},{label:'Orders processed',value:String(x.ordersProcessed),change:'Selected period'},{label:'Average order value',value:`$${Number(x.averageOrderValue).toFixed(2)}`,change:'Selected period'},{label:'Cancelled orders',value:String(x.cancelledOrders),change:'Selected period'}];const max=Math.max(1,...x.salesByMonth.map((m:any)=>m.total));this.months=x.salesByMonth.map((m:any)=>({month:`${m.month}/${m.year}`,width:Math.round(m.total/max*100)}));});}
}
