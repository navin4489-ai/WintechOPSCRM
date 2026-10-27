import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { InrPipe } from '../../pipes/inr.pipe';
import { ProductDto } from '../../models';

interface CartLine { productId:number; name:string; unitPrice:number; quantity:number; notes:string; modifierOptionIds:number[]; }
@Component({selector:'app-public-order',standalone:true,imports:[CommonModule,FormsModule,InrPipe],templateUrl:'./public-order.component.html',styleUrls:['./public-order.component.css']})
export class PublicOrderComponent implements OnInit {
  key=''; menu:any=null; loading=true; busy=false; error='';
  cart:CartLine[]=[]; customerName=''; notes=''; requestId=''; confirmation:any=null;
  search=''; category='All'; selected:ProductDto|null=null; quantity=1; itemNotes=''; modifiers:Record<number,boolean>={};
  constructor(private api:ApiService,private route:ActivatedRoute){}
  ngOnInit(){this.key=this.route.snapshot.paramMap.get('key')||'';try{const saved=JSON.parse(sessionStorage.getItem('oro_guest_'+this.key)||'null');if(saved){this.cart=saved.cart||[];this.customerName=saved.customerName||'';this.notes=saved.notes||'';this.requestId=saved.requestId||'';this.confirmation=saved.confirmation;}}catch{}this.load();}
  load(){this.loading=true;this.error='';this.api.publicMenu(this.key).subscribe({next:r=>{this.menu=r.data;this.loading=false;},error:e=>{this.loading=false;this.error=this.message(e);}});}
  get categories():string[]{return ['All',...new Set<string>((this.menu?.products||[]).map((p:ProductDto)=>p.categoryName||'Other'))];}
  get products():ProductDto[]{return (this.menu?.products||[]).filter((p:ProductDto)=>(this.category==='All'||(p.categoryName||'Other')===this.category)&&p.name.toLowerCase().includes(this.search.toLowerCase()));}
  get subtotal(){return this.cart.reduce((s,i)=>s+i.unitPrice*i.quantity,0);}
  get tax(){return Math.round(this.subtotal*5)/100;}
  open(product:ProductDto){this.selected=product;this.quantity=1;this.itemNotes='';this.modifiers={};this.error='';}
  add(){if(!this.selected)return;const p=this.selected;if(!Number.isInteger(this.quantity)||this.quantity<1||this.quantity>20){this.error='Choose a quantity between 1 and 20.';return;}const ids=Object.keys(this.modifiers).filter(k=>this.modifiers[+k]).map(Number);for(const g of p.modifierGroups||[]){const count=g.options.filter(o=>ids.includes(o.id)).length;if(count<Math.max(g.minSelections,g.isRequired?1:0)||count>g.maxSelections){this.error=`Check selections for ${g.name}.`;return;}}const extra=(p.modifierGroups||[]).flatMap(g=>g.options).filter(o=>ids.includes(o.id)).reduce((s,o)=>s+o.priceImpact,0);this.cart.push({productId:p.id,name:p.name,quantity:this.quantity,unitPrice:p.price+extra,modifierOptionIds:ids,notes:this.itemNotes});this.selected=null;this.changed();}
  remove(index:number){this.cart.splice(index,1);this.changed();}
  changed(){this.requestId='';this.persist();}
  persist(){sessionStorage.setItem('oro_guest_'+this.key,JSON.stringify({cart:this.cart,customerName:this.customerName,notes:this.notes,requestId:this.requestId,confirmation:this.confirmation}));}
  submit(){if(this.busy||!this.cart.length||!this.customerName.trim())return;this.busy=true;this.error='';if(!this.requestId)this.requestId=crypto.randomUUID();this.persist();this.api.placeGuestOrder(this.key,{requestId:this.requestId,customerName:this.customerName.trim(),notes:this.notes,items:this.cart.map(i=>({productId:i.productId,quantity:i.quantity,notes:i.notes,modifierOptionIds:i.modifierOptionIds}))}).subscribe({next:r=>{this.busy=false;this.confirmation=r.data;this.cart=[];this.persist();},error:e=>{this.busy=false;this.error=this.message(e)+' If the connection was interrupted, retry the same cart; your request key prevents duplicate orders.';}});}
  refreshStatus(){if(this.busy||!this.confirmation)return;this.busy=true;this.api.trackGuestOrder(this.key,this.confirmation.orderNumber,this.confirmation.trackingProof).subscribe({next:r=>{this.busy=false;this.confirmation=r.data;this.persist();},error:e=>{this.busy=false;this.error=this.message(e);}});}
  another(){this.confirmation=null;this.requestId='';this.cart=[];this.notes='';this.persist();this.load();}
  message(e:any){return e?.status===429?'Too many requests. Please wait a minute and try again.':e?.error?.errors?.[0]||'Unable to reach the outlet. Please refresh or contact the outlet.';}
}
