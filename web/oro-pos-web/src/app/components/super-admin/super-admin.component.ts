import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ToastService } from '../../services/toast.service';
import { OutletDto } from '../../models';
import {DocumentPrintService} from '../../services/document-print.service';

@Component({selector:'app-super-admin',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./super-admin.component.html',styleUrls:['./super-admin.component.css']})
export class SuperAdminComponent implements OnInit {
  outlets:OutletDto[]=[];selected:OutletDto|null=null;settings:any=null;loading=false;busy=false;
  own=false;adminUsername='';adminEmail='';adminPassword='';showPassword=false;
  credentials:{outletName:string;username:string;password:string;loginUrl:string}|null=null;
  constructor(private api:ApiService,public auth:AuthService,private toast:ToastService,private route:ActivatedRoute,private documents:DocumentPrintService){}
  get allowed(){return this.auth.getCurrentUser()?.roleName==='Owner'||(this.own&&this.auth.getCurrentUser()?.roleName==='StoreManager');}
  ngOnInit(){this.own=this.route.snapshot.data['ownOutlet']===true;if(this.allowed)this.load();}
  load(){this.loading=true;if(this.own)this.api.ownOutlet().subscribe({next:r=>{this.outlets=r.data?[r.data]:[];this.loading=false;if(r.data)this.edit(r.data);},error:e=>this.fail(e)});else this.api.superAdminOutlets().subscribe({next:r=>{this.outlets=r.data||[];this.loading=false;},error:e=>this.fail(e)});}
  create(){this.selected={id:0,name:'',address:'',phone:'',timezone:'Asia/Kolkata',currency:'INR',isOperating:true};this.settings=null;this.credentials=null;this.adminUsername=this.adminEmail='';this.generatePassword();}
  generatePassword(){const alphabet='ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#';this.adminPassword='Wt2!'+Array.from(crypto.getRandomValues(new Uint8Array(16)),n=>alphabet[n%alphabet.length]).join('');}
  suggestUsername(){if(!this.adminUsername&&this.selected?.name)this.adminUsername=(this.selected.name.toLowerCase().replace(/[^a-z0-9]+/g,'_').replace(/^_|_$/g,'').slice(0,40)||'outlet')+'_admin_'+crypto.getRandomValues(new Uint16Array(1))[0];}
  edit(outlet:OutletDto){this.selected={...outlet};this.settings=null;(this.own?this.api.ownOrdering():this.api.orderingSettings(outlet.id)).subscribe({next:r=>this.settings=r.data,error:e=>this.fail(e)});}
  save(){
    if(!this.selected||this.busy)return;this.busy=true;
    if(!this.selected.id){const password=this.adminPassword;this.api.onboardOutlet(this.selected,this.adminUsername,password,this.adminEmail).subscribe({next:r=>{this.busy=false;if(r.data){this.credentials={outletName:r.data.outlet.name,username:r.data.administrator.username,password,loginUrl:location.origin+'/login'};this.adminPassword='';this.load();this.edit(r.data.outlet);setTimeout(()=>document.querySelector('.credential-card')?.scrollIntoView({behavior:'smooth',block:'start'}),0);}this.toast.success('Outlet and its separate administrator account created.');},error:e=>this.fail(e)});return;}
    (this.own?this.api.updateOwnOutlet(this.selected):this.api.saveOutlet(this.selected)).subscribe({next:r=>{this.busy=false;this.toast.success('Outlet configuration saved.');this.load();if(r.data)this.edit(r.data);},error:e=>this.fail(e)});
  }
  shareCredentials(){if(!this.credentials)return;const c=this.credentials;navigator.clipboard.writeText(`Wintech POS — ${c.outletName}\nSign in: ${c.loginUrl}\nUser ID: ${c.username}\nInitial password: ${c.password}\nAccess: this outlet only. Please change the password after signing in.`).then(()=>this.toast.success('Login details copied. Share them securely.'),()=>this.toast.warning('Clipboard unavailable. Select the displayed details manually.'));}
  dismissCredentials(){this.credentials=null;this.showPassword=false;}
  toggle(enabled:boolean,rotate=false){if(!this.selected?.id||this.busy)return;if(rotate&&!confirm('Replace this outlet QR link? Existing printed codes will stop working.'))return;this.busy=true;(this.own?this.api.configureOwnOrdering(enabled,rotate):this.api.configureOrdering(this.selected.id,enabled,rotate)).subscribe({next:r=>{this.settings=r.data;this.busy=false;this.toast.success(rotate?'QR link rotated. Replace printed codes.':enabled?'Public ordering enabled.':'Public ordering disabled.');},error:e=>this.fail(e)});}
  download(){if(!this.settings?.qrPngBase64)return;const a=document.createElement('a');a.href='data:image/png;base64,'+this.settings.qrPngBase64;a.download=`${(this.selected?.name||'Outlet').replace(/[\\/:*?"<>|]/g,'_')}-QR.png`;a.click();}
  copy(){if(this.settings?.publicUrl)navigator.clipboard.writeText(this.settings.publicUrl).then(()=>this.toast.success('Ordering link copied.'),()=>this.toast.warning('Copy the displayed link manually.'));}
  useOutlet(){if(!this.own&&this.selected?.id){this.auth.selectOutlet(this.selected.id,this.selected.name);window.location.assign('/dashboard');}}
  printQr(){if(!this.selected?.id||!this.settings?.qrPngBase64||this.busy)return;const id=this.selected.id;const image=this.settings.qrPngBase64;const win=window.open('','_blank','width=520,height=760');if(!win){this.toast.warning('Allow popups to print the QR poster.');return;}win.document.body.textContent='Preparing QR poster…';this.busy=true;this.api.getOutlet(id).subscribe({next:r=>{this.busy=false;try{if(!r.data)throw new Error('Outlet details unavailable.');this.documents.qrPoster(win,r.data,image);}catch(e){win.close();this.toast.error((e as Error).message);}},error:e=>{win.close();this.fail(e);}});}
  fail(e:any){this.loading=this.busy=false;this.toast.error(e?.error?.errors?.[0]||'Outlet configuration failed. Check the fields and try again.');}
}
