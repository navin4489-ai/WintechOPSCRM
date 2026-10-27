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
}
