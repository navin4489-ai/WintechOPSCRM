import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { OutletDto } from '../../models';

@Component({selector:'app-super-admin', standalone:true, imports:[CommonModule,FormsModule], templateUrl:'./super-admin.component.html', styleUrls:['./super-admin.component.css']})
export class SuperAdminComponent implements OnInit {
  outlets: OutletDto[] = [];
  selected: OutletDto | null = null;
  settings: any = null;
  loading = false;
  busy = false;
  constructor(private api:ApiService, public auth:AuthService, private toast:ToastService) {}
  ngOnInit() { if (this.auth.getCurrentUser()?.roleName === 'Owner') this.load(); }
  load() { this.loading=true;this.api.superAdminOutlets().subscribe({next:r=>{this.outlets=r.data||[];this.loading=false;},error:e=>this.fail(e)}); }
  create() { this.selected={id:0,name:'',address:'',phone:'',timezone:'Asia/Kolkata',currency:'INR',isOperating:true};this.settings=null; }
  edit(outlet:OutletDto) {this.selected={...outlet};this.settings=null;this.api.orderingSettings(outlet.id).subscribe({next:r=>this.settings=r.data,error:e=>this.fail(e)});}
  save() {if(!this.selected||this.busy)return;this.busy=true;this.api.saveOutlet(this.selected).subscribe({next:r=>{this.busy=false;this.toast.success('Outlet configuration saved.');this.load();if(r.data)this.edit(r.data);},error:e=>this.fail(e)});}
  toggle(enabled:boolean, rotate=false) {if(!this.selected?.id||this.busy)return;if(rotate&&!confirm('Replace this outlet QR link? Existing printed codes will stop working.'))return;this.busy=true;this.api.configureOrdering(this.selected.id,enabled,rotate).subscribe({next:r=>{this.settings=r.data;this.busy=false;this.toast.success(rotate?'QR link rotated. Replace printed codes.':enabled?'Public ordering enabled.':'Public ordering disabled.');},error:e=>this.fail(e)});}
  download() {if(!this.settings?.qrPngBase64)return;const a=document.createElement('a');a.href='data:image/png;base64,'+this.settings.qrPngBase64;a.download=`ORO-outlet-${this.selected?.id}-QR.png`;a.click();}
  copy() {if(this.settings?.publicUrl)navigator.clipboard.writeText(this.settings.publicUrl).then(()=>this.toast.success('Ordering link copied.'),()=>this.toast.warning('Copy the displayed link manually.'));}
  useOutlet() {if(this.selected?.id){this.auth.selectOutlet(this.selected.id,this.selected.name);window.location.assign('/dashboard');}}
  fail(e:any) {this.loading=this.busy=false;this.toast.error(e?.error?.errors?.[0]||'Outlet configuration failed.');}
}
