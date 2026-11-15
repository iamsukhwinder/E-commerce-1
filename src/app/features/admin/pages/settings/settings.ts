import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../services/admin-api.service';
@Component({selector:'app-admin-settings',standalone:true,imports:[FormsModule],templateUrl:'./settings.html',styleUrl:'./settings.scss'})
export class AdminSettings implements OnInit { private readonly api=inject(AdminApiService); storeName='Dista Marketplace'; supportEmail='support@example.com'; currency='USD'; timezone='Asia/Kolkata'; saved=false; ngOnInit():void{this.api.settings().subscribe(x=>{this.storeName=x['storeName']||this.storeName;this.supportEmail=x['supportEmail']||this.supportEmail;this.currency=x['currency']||this.currency;this.timezone=x['timezone']||this.timezone;});} save():void{this.api.saveSettings({storeName:this.storeName,supportEmail:this.supportEmail,currency:this.currency,timezone:this.timezone}).subscribe(()=>this.saved=true);} }
