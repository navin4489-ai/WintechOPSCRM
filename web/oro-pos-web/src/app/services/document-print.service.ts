import {Injectable} from '@angular/core';
import {OrderDto,ZReportDto,OutletDto} from '../models';
import {InrPipe} from '../pipes/inr.pipe';

export const documentText=(value:unknown)=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
interface OutletHeader {outletName?:string;outletAddress?:string;outletPhone?:string;}
@Injectable({providedIn:'root'})
export class DocumentPrintService {
  private render(win:Window,outlet:OutletHeader,title:string,body:string){
    const name=outlet.outletName?.trim();
    if(!name)throw new Error('Outlet details are unavailable. Refresh the document before printing.');
    const esc=documentText;
    win.document.open();
    win.document.write(`<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${esc(name)} - ${esc(title)}</title><style>body{font:14px system-ui,sans-serif;max-width:400px;margin:24px auto;padding:0 12px;color:#111;box-sizing:border-box}.outlet-header{text-align:center;border-bottom:1px dashed #888;padding-bottom:16px;margin-bottom:18px}.outlet-header h1{font-size:24px;font-weight:800;margin:0 0 8px;overflow-wrap:anywhere}.outlet-header p{margin:4px 0;white-space:pre-line;overflow-wrap:anywhere}table{width:100%;border-collapse:collapse}td{padding:8px 0;border-bottom:1px solid #ddd}td:last-child{text-align:right}h2{font-size:20px}p{overflow-wrap:anywhere}@page{margin:12mm}@media print{body{max-width:none;margin:0}}</style></head><body><header class="outlet-header"><h1>${esc(name)}</h1><p class="outlet-address">${esc(outlet.outletAddress?.trim()||'Address not configured')}</p><p class="outlet-contact">Contact: ${esc(outlet.outletPhone?.trim()||'Not configured')}</p></header>${body}</body></html>`);
    win.document.close();win.focus();win.print();
  }
  receipt(win:Window,order:OrderDto){
    const esc=documentText;const inr=(v:number)=>esc(new InrPipe().transform(v));
    this.render(win,order,`Receipt ${order.orderNumber}`,`<h2>Transaction receipt</h2><p>${esc(order.orderNumber)} · ${esc(order.status)}</p><p>${esc(order.serviceType)} · Table ${esc(order.tableNumber||'—')}</p><table>${order.orderItems.map(i=>`<tr><td>${esc(i.quantity)} × ${esc(i.productName)}</td><td>${inr(i.lineTotal)}</td></tr>`).join('')}</table><p>Subtotal: ${inr(order.subtotal)}</p><p>Discount: ${inr(order.discountAmount)}</p><p>GST: ${inr(order.taxAmount)}</p><h2>Total: ${inr(order.totalAmount)}</h2><p>Thank you. Keep this receipt for your records.</p>`);
  }
  zReport(report:ZReportDto){
    const win=window.open('','_blank','width=520,height=760');if(!win)throw new Error('Allow popups to print the report.');
    const esc=documentText;const rows:{[label:string]:number}={'Orders':report.orderCount,'Gross sales':report.grossSales,'Discounts':report.discounts,'GST':report.taxes,'Net sales':report.netSales,'Opening float':report.openingFloat,'Expected cash':report.cashExpected,'Counted cash':report.cashCounted,'Variance':report.variance};
    try{this.render(win,report,`Z-report ${report.shiftId}`,`<h2>Z-report · Shift #${esc(report.shiftId)}</h2><p>Cashier: ${esc(report.userName)}</p><p>Opened: ${esc(new Date(report.openTime).toLocaleString('en-IN'))}</p><p>Closed: ${report.closeTime?esc(new Date(report.closeTime).toLocaleString('en-IN')):'Open shift'}</p><table>${Object.entries(rows).map(([k,v])=>`<tr><td>${esc(k)}</td><td>${k==='Orders'?esc(v):esc(new InrPipe().transform(v))}</td></tr>`).join('')}</table>`);}catch(e){win.close();throw e;}
  }
  qrPoster(win:Window,outlet:OutletDto,pngBase64:string){
    this.render(win,{outletName:outlet.name,outletAddress:outlet.address,outletPhone:outlet.phone},'Ordering QR',`<section class="qr-poster" style="text-align:center"><img style="width:280px;max-width:100%;margin:24px auto" src="data:image/png;base64,${documentText(pngBase64)}" alt="Scan to order"/><h2>Scan to order</h2><p>Pay cash at the outlet</p></section>`);
  }
  sales(win:Window,outlet:OutletDto,rows:any[]){
    const esc=documentText;const keys=['grossSales','discounts','taxes','netSales','cash','card','digitalWallet'];
    this.render(win,{outletName:outlet.name,outletAddress:outlet.address,outletPhone:outlet.phone},'Sales report INR',`<h2>Sales report (INR)</h2><table style="font-size:10px"><thead><tr>${['Group','Orders','Gross','Discount','GST','Net','Cash','Card','UPI'].map(h=>`<th style="text-align:left;padding:6px">${h}</th>`).join('')}</tr></thead><tbody>${rows.map(r=>`<tr><td>${esc(r.groupKey)}</td><td>${esc(r.orderCount)}</td>${keys.map(k=>`<td>${esc(Number(r[k]||0).toFixed(2))}</td>`).join('')}</tr>`).join('')}</tbody></table>`);
  }
}
