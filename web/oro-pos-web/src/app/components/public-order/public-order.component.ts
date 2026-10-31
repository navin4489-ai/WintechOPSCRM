import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../services/api.service';
import { InrPipe } from '../../pipes/inr.pipe';
import { ProductDto } from '../../models';

interface CartLine { productId:number; name:string; unitPrice:number; quantity:number; notes:string; modifierOptionIds:number[]; }
@Component({selector:'app-public-order',standalone:true,imports:[CommonModule,FormsModule,InrPipe],templateUrl:'./public-order.component.html',styleUrls:['./public-order.component.css']})
export class PublicOrderComponent implements OnInit,OnDestroy {
  key=''; menu:any=null; loading=true; busy=false; error='';
  cart:CartLine[]=[]; customerName=''; customerPhone=''; notes=''; requestId=''; confirmation:any=null;
  search=''; category='All'; selected:ProductDto|null=null; quantity=1; itemNotes=''; modifiers:Record<number,boolean>={};
  cartOpen=false;
  private poll?:ReturnType<typeof setInterval>;
  lastUpdated:Date|null=null;statusNote='';
  private printLocation:string|null=null;
  constructor(private api:ApiService,private route:ActivatedRoute){}
  ngOnInit(){
    this.key=this.route.snapshot.paramMap.get('key')||'';
    try{const saved=JSON.parse(sessionStorage.getItem('oro_guest_'+this.key)||'null');if(saved){this.cart=saved.cart||[];this.customerName=saved.customerName||'';this.customerPhone=saved.customerPhone||'';this.notes=saved.notes||'';this.requestId=saved.requestId||'';this.confirmation=saved.confirmation;}}catch{}
    try{const encoded=new URLSearchParams(this.route.snapshot.fragment||'').get('track');if(encoded){const t=JSON.parse(atob(encoded));if(typeof t.number==='string'&&t.number.length<100&&/^[a-f0-9]{64}$/i.test(t.proof))this.confirmation={orderNumber:t.number,trackingProof:t.proof};}}catch{}
    this.load();if(this.confirmation)this.refreshStatus();
    this.poll=setInterval(()=>{if(!document.hidden&&!this.busy&&this.printLocation===null&&this.confirmation&&!['Voided','Refunded'].includes(this.confirmation.status)&&!(this.confirmation.status==='Closed'&&this.confirmation.preparationStatus==='Served'))this.refreshStatus(true);},15000);
  }
  ngOnDestroy(){if(this.poll)clearInterval(this.poll);}
  get trackingUrl(){if(!this.confirmation?.trackingProof)return '';const url=new URL(location.origin+'/order/'+this.key);const p=new URLSearchParams();p.set('track',btoa(JSON.stringify({number:this.confirmation.orderNumber,proof:this.confirmation.trackingProof})));url.hash=p.toString();return url.toString();}
  copyTracking(){navigator.clipboard.writeText(this.trackingUrl).then(()=>this.statusNote='Tracking link copied. Save or bookmark it; anyone with this private link can see this order.',()=>this.statusNote='Clipboard unavailable. Bookmark this page using your browser.');}
  load(){this.loading=true;this.error='';this.api.publicMenu(this.key).subscribe({next:r=>{this.menu=r.data;this.loading=false;this.titleForOutlet();},error:e=>{this.loading=false;this.error=this.message(e);}});}
  private titleForOutlet(){document.title=(this.menu?.outletName||this.confirmation?.outletName||'Outlet')+(this.confirmation?' - Order '+this.confirmation.orderNumber.slice(-8).toUpperCase():' - Menu');}
  @HostListener('window:beforeprint') beforePrint(){if(this.printLocation===null)this.printLocation=location.href;history.replaceState(null,'',location.pathname+location.search);}
  @HostListener('window:afterprint') afterPrint(){if(this.printLocation){history.replaceState(null,'',this.printLocation);this.printLocation=null;}}
  get categories():string[]{return ['All',...new Set<string>((this.menu?.products||[]).map((p:ProductDto)=>p.categoryName||'Other'))];}
  get products():ProductDto[]{return (this.menu?.products||[]).filter((p:ProductDto)=>(this.category==='All'||(p.categoryName||'Other')===this.category)&&p.name.toLowerCase().includes(this.search.toLowerCase()));}
  get subtotal(){return this.cart.reduce((s,i)=>s+i.unitPrice*i.quantity,0);}
  get tax(){return Math.round(this.subtotal*5)/100;}
  open(product:ProductDto){this.selected=product;this.quantity=1;this.itemNotes='';this.modifiers={};this.error='';}
  add(){if(!this.selected)return;const p=this.selected;if(!Number.isInteger(this.quantity)||this.quantity<1||this.quantity>20){this.error='Choose a quantity between 1 and 20.';return;}const ids=Object.keys(this.modifiers).filter(k=>this.modifiers[+k]).map(Number);for(const g of p.modifierGroups||[]){const count=g.options.filter(o=>ids.includes(o.id)).length;if(count<Math.max(g.minSelections,g.isRequired?1:0)||count>g.maxSelections){this.error=`Check selections for ${g.name}.`;return;}}const extra=(p.modifierGroups||[]).flatMap(g=>g.options).filter(o=>ids.includes(o.id)).reduce((s,o)=>s+o.priceImpact,0);this.cart.push({productId:p.id,name:p.name,quantity:this.quantity,unitPrice:p.price+extra,modifierOptionIds:ids,notes:this.itemNotes});this.selected=null;this.changed();}
  remove(index:number){this.cart.splice(index,1);this.changed();}
  changed(){this.requestId='';this.persist();}
  persist(){sessionStorage.setItem('oro_guest_'+this.key,JSON.stringify({cart:this.cart,customerName:this.customerName,customerPhone:this.confirmation?'':this.customerPhone,notes:this.notes,requestId:this.requestId,confirmation:this.confirmation}));}
  submit(){
    if(this.busy||!this.cart.length||!this.customerName.trim()||!this.customerPhone.trim())return;
    const digits=this.customerPhone.replace(/[^0-9]/g,'');if(digits.length<7||digits.length>15){this.error='Enter a valid contact number with 7–15 digits.';return;}
    this.busy=true;this.error='';if(!this.requestId)this.requestId=crypto.randomUUID();this.persist();
    this.api.placeGuestOrder(this.key,{requestId:this.requestId,customerName:this.customerName.trim(),customerPhone:this.customerPhone.trim(),notes:this.notes,items:this.cart.map(i=>({productId:i.productId,quantity:i.quantity,notes:i.notes,modifierOptionIds:i.modifierOptionIds}))}).subscribe({
      next:r=>{this.busy=false;this.confirmation=r.data;this.cart=[];this.customerPhone='';this.cartOpen=false;this.lastUpdated=new Date();this.persist();this.titleForOutlet();history.replaceState(null,'',this.trackingUrl);},
      error:e=>{this.busy=false;this.error=this.message(e)+' If the connection was interrupted, retry the same cart; your request key prevents duplicate orders.';}
    });
  }
  refreshStatus(silent=false){if(this.busy||!this.confirmation)return;this.busy=true;this.api.trackGuestOrder(this.key,this.confirmation.orderNumber,this.confirmation.trackingProof).subscribe({next:r=>{this.busy=false;this.confirmation=r.data;this.lastUpdated=new Date();this.statusNote='';if(!this.menu)this.menu={outletName:r.data?.outletName,phone:r.data?.outletPhone,products:[],acceptingOrders:false,paymentInstruction:'Cash payments are handled at the outlet.'};this.persist();this.titleForOutlet();history.replaceState(null,'',this.trackingUrl);},error:e=>{this.busy=false;if(silent)this.statusNote='Status refresh is temporarily unavailable. We will retry automatically.';else this.error=this.message(e);}});}
  another(){this.confirmation=null;this.requestId='';this.cart=[];this.notes='';this.statusNote='';history.replaceState(null,'',location.pathname);this.persist();this.load();}
  viewCart(){this.cartOpen=true;}
  closeCart(){this.cartOpen=false;}
  @HostListener('document:keydown.escape') escapeCart(){this.closeCart();}
  // How many of this item are already in the order — shown on the card so a
  // long menu still tells the customer what they have picked.
  qtyIn(productId:number):number{const line=this.cart.find(i=>i.productId===productId);return line?line.quantity:0;}
  get cartCount():number{return this.cart.reduce((s,i)=>s+i.quantity,0);}
  message(e:any){return e?.status===429?'Too many requests. Please wait a minute and try again.':e?.error?.errors?.[0]||'Unable to reach the outlet. Please refresh or contact the outlet.';}
}
