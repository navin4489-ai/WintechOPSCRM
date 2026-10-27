import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import { ApiResponse } from '../../models';
import { InrPipe } from '../../pipes/inr.pipe';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [CommonModule, FormsModule, InrPipe],
  templateUrl: './menu.component.html',
  styleUrls: ['./menu.component.css']
})
export class MenuComponent implements OnInit {
  products: any[] = [];
  categories: any[] = [];
  editing: any = null;
  busy = false;
  uploadingId: number | null = null;
  categoryDialog=false;categoryBusy=false;categoryName='';categorySort=0;categoryError='';
  get canEdit() { const u=this.auth.getCurrentUser(); return u?.roleName==='Owner'||u?.roleName==='StoreManager'||!!u?.permissions?.includes('ManageSettings'); }

  constructor(private api: ApiService, private auth: AuthService, private toast: ToastService) {}

  ngOnInit() {
    const outletId = this.auth.getCurrentUser()?.outletId || 0;
    this.api.getProducts(outletId, undefined, false).subscribe((res: ApiResponse<any[]>) => { if (res.success) this.products = res.data || []; });
    this.api.getCategories(outletId).subscribe((res: ApiResponse<any[]>) => { if (res.success) this.categories = res.data || []; });
  }
  edit(p?:any) {this.editing=p?{...p,modifierGroupIds:(p.modifierGroups||[]).map((g:any)=>g.id)}:{id:0,outletId:this.auth.getCurrentUser()?.outletId,name:'',sku:'',price:0,categoryId:null,isAvailable:true,sortOrder:0,modifierGroupIds:[]};}
  save() {if(!this.editing||this.busy)return;this.busy=true;this.api.saveProduct(this.editing.id,this.editing).subscribe({next:()=>{this.busy=false;this.editing=null;this.ngOnInit();this.toast.success('Item saved.');},error:e=>{this.busy=false;this.toast.error(e?.error?.errors?.[0]||'Unable to save item.');}});}
  upload(p:any,event:Event) {const input=event.target as HTMLInputElement;const file=input.files?.[0];if(!file)return;if(file.size>1024*1024||!['image/png','image/jpeg','image/webp'].includes(file.type)){this.toast.warning('Choose a PNG, JPEG or WebP image up to 1 MB.');input.value='';return;}this.uploadingId=p.id;this.api.uploadProductImage(p.id,file).subscribe({next:r=>{p.imageUrl=r.data?.imageUrl;this.uploadingId=null;input.value='';this.toast.success('Product image uploaded.');},error:e=>{this.uploadingId=null;input.value='';this.toast.error(e?.error?.errors?.[0]||'Image upload failed.');}});}
  removeImage(p:any) {if(!confirm('Remove this product image?'))return;this.api.removeProductImage(p.id).subscribe({next:()=>{p.imageUrl=null;this.toast.success('Image removed.');},error:e=>this.toast.error(e?.error?.errors?.[0]||'Unable to remove image.')});}
  newCategory(){this.categoryDialog=true;this.categoryName='';this.categorySort=this.categories.length+1;this.categoryError='';}
  saveCategory(){if(!this.canEdit||this.categoryBusy)return;const name=this.categoryName.trim();if(!name||name.length>80){this.categoryError='Enter a category name (1–80 characters).';return;}const outletId=this.auth.getCurrentUser()?.outletId;if(!outletId){this.categoryError='Select your outlet workspace first.';return;}this.categoryBusy=true;this.categoryError='';this.api.createCategory({outletId,name,sortOrder:this.categorySort}).subscribe({next:r=>{this.categoryBusy=false;if(r.data){this.categories=[...this.categories,r.data].sort((a,b)=>(a.sortOrder||0)-(b.sortOrder||0));if(this.editing)this.editing.categoryId=r.data.id;this.categoryDialog=false;this.toast.success('Category created for this outlet.');}},error:e=>{this.categoryBusy=false;this.categoryError=e?.error?.errors?.[0]||'Category creation failed. Please retry.';}});}
}
