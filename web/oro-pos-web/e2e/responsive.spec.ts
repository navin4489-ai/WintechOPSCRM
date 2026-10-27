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
