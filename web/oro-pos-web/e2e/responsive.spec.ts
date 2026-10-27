import { test,expect,Page } from '@playwright/test';

const outlet={id:10,name:'Responsive Outlet',address:'Test address',phone:'9999999999',timezone:'Asia/Kolkata',currency:'INR',isOperating:true};
const permissions=['CreateOrder','EditOrder','VoidOrder','RefundOrder','ApplyDiscount','ManageUsers','ManageInventory','ViewReports','ExportReports','CloseShift','ApproveManagerAction'];
async function setup(page:Page,role?:'Owner'|'StoreManager'){
  const categories=[{id:1,outletId:10,name:'Starters',sortOrder:1}];
  if(role)await page.addInitScript(({role,permissions,outlet})=>{localStorage.setItem('oro_pos_token','synthetic-ui-test-token');localStorage.setItem('oro_pos_user',JSON.stringify({id:1,username:'ui_operator',roleName:role,permissions,outletId:outlet.id,outletName:outlet.name,isActive:true}));},{role,permissions,outlet});
  await page.route('**/api/**',async route=>{
    const url=new URL(route.request().url());const path=url.pathname;const method=route.request().method();let data:any=[];
    if(path==='/api/super-admin/outlets'&&method==='POST'){const body=route.request().postDataJSON();data={outlet:{...body.outlet,id:30},administrator:{id:5,username:body.adminUsername,roleName:'StoreManager',outletId:30}};}
    else if(path==='/api/super-admin/outlets'||path==='/api/outlets')data=[outlet];
    else if(path==='/api/outlet-settings'||path==='/api/outlets/10')data=outlet;
    else if(path.endsWith('/ordering'))data={enabled:false,cashOnly:true};
    else if(path.includes('/public/outlets/')&&path.endsWith('/menu'))data={outletName:outlet.name,address:outlet.address,phone:outlet.phone,acceptingOrders:true,paymentInstruction:'Pay cash at the outlet.',products:[{id:1,name:'Paneer Tikka',price:280,sku:'P1',categoryName:'Starters',isAvailable:true,modifierGroups:[]}]};
    else if(path.includes('/public/outlets/')&&path.endsWith('/orders'))data={orderNumber:'WEB-12345678901234567890123456789012',trackingProof:'c'.repeat(64),outletName:outlet.name,outletPhone:outlet.phone,status:'Open',preparationStatus:'New',subtotal:280,taxAmount:14,totalAmount:294,paymentStatus:'Pay cash at the outlet'};
    else if(path.endsWith('/status'))data={orderNumber:'WEB-12345678901234567890123456789012',trackingProof:'c'.repeat(64),outletName:outlet.name,outletPhone:outlet.phone,status:'Open',preparationStatus:'Ready',subtotal:280,taxAmount:14,totalAmount:294,paymentStatus:'Pay cash at the outlet'};
    else if(path==='/api/orders')data={items:[],totalCount:0,page:1,pageSize:20};
    else if(path==='/api/reports/dashboard')data={todayOrders:0,todaySales:0,todayCash:0,openTables:0,pendingKitchenTickets:0,lowStockItems:0};
    else if(path==='/api/shifts/open')data=null;
    else if(path==='/api/products')data=[{id:1,outletId:10,categoryId:1,categoryName:'Starters',name:'Paneer Tikka',sku:'P1',price:280,isAvailable:true,modifierGroups:[]}];
    else if(path==='/api/categories/outlet/10')data=categories;
    else if(path==='/api/categories'&&method==='POST'){data={...route.request().postDataJSON(),id:categories.length+1};categories.push(data);}
    else if(path==='/api/roles')data=[{id:1,name:'Owner',permissions:[]},{id:2,name:'StoreManager',permissions}];
    await route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data})});
  });
}
async function fits(page:Page){
  const result=await page.evaluate(()=>({width:innerWidth,documentWidth:document.documentElement.scrollWidth,overflow:[...document.querySelectorAll('input,select,textarea,button')].filter(e=>{const el=e as HTMLElement;const s=getComputedStyle(el);const b=el.getBoundingClientRect();return s.visibility!=='hidden'&&s.display!=='none'&&b.width>0&&!el.closest('.table-wrap')&&(b.left< -2||b.right>innerWidth+2);}).map(e=>({text:e.textContent?.slice(0,40),tag:e.tagName}))}));
  expect(result.documentWidth).toBeLessThanOrEqual(result.width+1);expect(result.overflow).toEqual([]);
}
for(const width of [375,390,430]){
  test(`Super Admin onboarding and credentials fit ${width}px`,async({page},info)=>{
    await page.setViewportSize({width,height:844});await setup(page,'Owner');await page.goto('/super-admin');
    await expect(page.getByRole('heading',{name:'Super Admin · Outlets'})).toBeVisible();await page.getByRole('button',{name:'Add outlet + admin login'}).click();
    await page.getByLabel('Outlet name',{exact:true}).fill('New mobile outlet');await page.getByLabel('User ID',{exact:true}).fill('new_outlet_admin');await fits(page);
    await page.getByRole('button',{name:'Create outlet + admin account',exact:true}).click();
    await expect(page.getByRole('heading',{name:'Outlet admin login created'})).toBeVisible();await fits(page);
    await page.locator('.credential-card').scrollIntoViewIfNeeded();await page.screenshot({path:info.outputPath(`onboarding-${width}.png`),fullPage:true});
  });
  test(`Outlet admin own settings and navigation fit ${width}px`,async({page},info)=>{
    await page.setViewportSize({width,height:844});await setup(page,'StoreManager');await page.goto('/outlet-settings');
    await expect(page.getByRole('heading',{name:'My outlet settings'})).toBeVisible();await expect(page.getByRole('button',{name:'Add outlet + admin login'})).toHaveCount(0);await fits(page);
    await page.getByRole('button',{name:'Open navigation',exact:true}).click();await expect(page.getByRole('link',{name:'My Outlet Settings',exact:true})).toBeVisible();await expect(page.getByRole('link',{name:'Super Admin · Outlets',exact:true})).toHaveCount(0);
    await page.getByRole('link',{name:'My Outlet Settings',exact:true}).click();await expect(page.locator('.sidebar')).not.toHaveClass(/open/);await expect(page.locator('.sidebar')).toBeHidden();await fits(page);await page.screenshot({path:info.outputPath(`outlet-admin-${width}.png`),fullPage:true});
  });
}
test('QR phone checkout and private preparation tracking fit 390px',async({page},info)=>{
  await page.setViewportSize({width:390,height:844});await setup(page);await page.goto('/order/'+'a'.repeat(64));
  await page.getByRole('button',{name:'Add',exact:true}).click();await page.getByRole('button',{name:'Add to order',exact:true}).click();await page.getByRole('button',{name:/View your order/}).click();
  await page.getByLabel('Your name',{exact:true}).fill('UI test guest');await page.getByLabel('Contact number',{exact:true}).fill('9999999999');await fits(page);
  await page.getByRole('button',{name:'Place order — pay at outlet',exact:true}).click();await expect(page.getByRole('heading',{name:'Order received'})).toBeVisible();expect(page.url()).toContain('#track=');
  await page.getByRole('button',{name:'Refresh status',exact:true}).click();await expect(page.locator('.confirmation')).toContainText('Ready');await expect(page.locator('.confirmation')).not.toContainText('9999999999');await fits(page);await page.screenshot({path:info.outputPath('customer-tracking-390.png'),fullPage:true});
  const savedLink=page.url();const reopened=await page.context().newPage();await reopened.setViewportSize({width:390,height:844});await setup(reopened);await reopened.goto(savedLink);await expect(reopened.locator('.confirmation')).toContainText('Ready');await fits(reopened);await reopened.close();
});
for(const path of ['/dashboard','/pos','/orders','/inventory','/menu','/staff','/reports','/shifts'])test(`Operational page ${path} fits 390px`,async({page})=>{
  await page.setViewportSize({width:390,height:844});await setup(page,'StoreManager');await page.goto(path);await expect(page.locator('.content')).toBeVisible();await page.waitForTimeout(500);await fits(page);
});
test('Outlet admin creates a category inside the item editor on a phone',async({page})=>{
  await page.setViewportSize({width:390,height:844});await setup(page,'StoreManager');await page.goto('/menu');
  await page.getByRole('button',{name:'Add item',exact:true}).click();await page.getByRole('button',{name:'New category',exact:true}).click();
  await page.getByLabel('Category name',{exact:true}).fill('Fresh breads');await fits(page);
  const [response]=await Promise.all([page.waitForResponse(r=>r.url().endsWith('/api/categories')&&r.request().method()==='POST'),page.getByRole('button',{name:'Create category',exact:true}).click()]);
  expect(response.request().postDataJSON().outletId).toBe(10);await expect(page.locator('.category-overlay')).toHaveCount(0);
  await expect(page.locator('select[name=categoryId] option:checked')).toHaveText('Fresh breads');await fits(page);
});
test('Printed receipt uses the order outlet header, never the operator or platform',async({page},info)=>{
  await setup(page,'Owner');
  const bill={id:42,outletId:20,outletName:'Receipt Outlet B',outletAddress:'22 Market Road',outletPhone:'8888888888',orderNumber:'BILL-0042',createdAt:'2026-10-27T12:00:00Z',status:'Closed',serviceType:'Takeaway',subtotal:100,discountAmount:0,taxAmount:5,totalAmount:105,orderItems:[{id:1,quantity:1,productName:'Meal',lineTotal:100}],payments:[{status:'Captured',amount:105}]};
  await page.route('**/api/orders**',route=>route.fulfill({status:200,contentType:'application/json',body:JSON.stringify({success:true,data:new URL(route.request().url()).pathname==='/api/orders'?{items:[bill],page:1,pageSize:20,totalCount:1}:bill})}));
  await page.goto('/orders');await page.getByRole('button',{name:'View bill',exact:true}).click();
  await expect(page.locator('.outlet-bill-header')).toContainText('Receipt Outlet B');
  const created=page.waitForEvent('popup');await page.getByRole('button',{name:'Print receipt',exact:true}).click();const popup=await created;
  await expect(popup.locator('.outlet-header h1')).toHaveText('Receipt Outlet B');await expect(popup.locator('.outlet-address')).toHaveText('22 Market Road');await expect(popup.locator('.outlet-contact')).toContainText('8888888888');
  expect(await popup.locator('.outlet-header').evaluate(e=>getComputedStyle(e).textAlign)).toBe('center');expect(Number(await popup.locator('.outlet-header h1').evaluate(e=>getComputedStyle(e).fontWeight))).toBeGreaterThanOrEqual(700);
  await expect(popup.locator('body')).not.toContainText('ORO');await expect(popup.locator('body')).not.toContainText('Wintech');await expect(popup.locator('body')).not.toContainText('Responsive Outlet');
  await popup.pdf({path:info.outputPath('outlet-receipt.pdf'),format:'A4'});await popup.close();
});
test('Shift print/PDF contains only its actual outlet branding',async({page},info)=>{
  await setup(page,'Owner');
  await page.route('**/api/shifts?**',route=>route.fulfill({json:{success:true,data:[{id:7,userName:'cashier',status:'Closed',openTime:'2026-10-27T10:00:00Z',closeTime:'2026-10-27T15:00:00Z',openingFloat:500,variance:0}]}}));
  await page.route('**/api/shifts/7/z-report',route=>route.fulfill({json:{success:true,data:{shiftId:7,outletId:30,outletName:'Shift Outlet C',outletAddress:'3 Station Road',outletPhone:'7777777777',userName:'cashier',openTime:'2026-10-27T10:00:00Z',closeTime:'2026-10-27T15:00:00Z',openingFloat:500,grossSales:100,discounts:0,taxes:5,netSales:100,cashExpected:605,cashCounted:605,variance:0,orderCount:1}}}));
  await page.goto('/shifts');await page.getByRole('button',{name:'Z-report',exact:true}).click();const created=page.waitForEvent('popup');await page.getByRole('button',{name:'Print / PDF',exact:true}).click();const popup=await created;
  await expect(popup.locator('.outlet-header h1')).toHaveText('Shift Outlet C');await expect(popup.locator('body')).toContainText('3 Station Road');await expect(popup.locator('body')).toContainText('7777777777');await expect(popup.locator('body')).not.toContainText('Wintech');await expect(popup.locator('body')).not.toContainText('ORO');await popup.pdf({path:info.outputPath('outlet-z-report.pdf'),format:'A4'});await popup.close();
});
test('QR preview and printed poster never display a URL below the code',async({page},info)=>{
  await setup(page,'StoreManager');
  const png='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC';
  await page.route('**/api/outlet-settings/ordering',route=>route.fulfill({json:{success:true,data:{enabled:true,publicUrl:'https://ordering.example.invalid/private-menu-link',qrPngBase64:png,cashOnly:true}}}));
  await page.goto('/outlet-settings');await expect(page.locator('.qr img')).toBeVisible();await expect(page.locator('.qr')).not.toContainText('https://');await expect(page.getByRole('button',{name:'Copy link',exact:true})).toBeVisible();
  const created=page.waitForEvent('popup');await page.getByRole('button',{name:'Print QR / PDF',exact:true}).click();const popup=await created;
  await expect(popup.locator('.outlet-header h1')).toHaveText(outlet.name);await expect(popup.locator('body')).toContainText(outlet.address);await expect(popup.locator('body')).toContainText(outlet.phone);await expect(popup.locator('body')).not.toContainText('https://');await expect(popup.locator('body')).not.toContainText('ordering.example');await expect(popup.locator('body')).not.toContainText('ORO');await expect(popup.locator('body')).not.toContainText('Wintech');await popup.pdf({path:info.outputPath('outlet-qr-poster.pdf'),format:'A4'});await popup.close();
});
test('Sales report PDF uses outlet name, address and contact without platform branding',async({page},info)=>{
  await setup(page,'Owner');await page.goto('/reports');const created=page.waitForEvent('popup');await page.getByRole('button',{name:'Print / PDF',exact:true}).click();const popup=await created;
  await expect(popup.locator('.outlet-header h1')).toHaveText(outlet.name);await expect(popup.locator('body')).toContainText(outlet.address);await expect(popup.locator('body')).toContainText(outlet.phone);await expect(popup.locator('body')).not.toContainText('Wintech');await expect(popup.locator('body')).not.toContainText('ORO');await popup.pdf({path:info.outputPath('outlet-sales-report.pdf'),format:'A4'});await popup.close();
});
test('Customer confirmation Save to PDF hides platform branding and private URL proof',async({page},info)=>{
  await setup(page);await page.goto('/order/'+'a'.repeat(64));await page.getByRole('button',{name:'Add',exact:true}).click();await page.getByRole('button',{name:'Add to order',exact:true}).click();await page.getByLabel('Your name',{exact:true}).fill('UI guest');await page.getByLabel('Contact number',{exact:true}).fill('9999999999');await page.getByRole('button',{name:'Place order — pay at outlet',exact:true}).click();await expect(page.getByRole('heading',{name:'Order received'})).toBeVisible();const saved=page.url();
  await page.evaluate(()=>window.dispatchEvent(new Event('beforeprint')));expect(page.url()).not.toContain('#track=');await page.emulateMedia({media:'print'});await expect(page.locator('.public-print-header')).toBeVisible();await expect(page.locator('.public-print-header h1')).toHaveText(outlet.name);await expect(page.locator('.guest>footer')).toBeHidden();await expect(page.locator('.guest>header img')).toBeHidden();expect(await page.title()).not.toMatch(/Wintech|ORO/);await page.pdf({path:info.outputPath('customer-confirmation.pdf'),format:'A4'});await page.evaluate(()=>window.dispatchEvent(new Event('afterprint')));expect(page.url()).toBe(saved);
});
