using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Enums;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.Tests;
public class TenantIsolationTests
{
    private sealed class Fixture : IDisposable
    {
        public readonly AppDbContext Db=new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly IConfiguration Config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Jwt:Key"]="tenant-isolation-test-key-with-at-least-32-bytes"}).Build();
        public AuthService Auth=null!;public UserService Users=null!;public OutletService Outlets=null!;public MenuService Menu=null!;public TableService Tables=null!;public InventoryService Inventory=null!;public ShiftService Shifts=null!;public ReportService Reports=null!;public KitchenService Kitchen=null!;public OrderService Orders=null!;public OutletProvisioningService Provision=null!;
        public OutletOnboardingResult A=null!,B=null!;public ProductDto PA=null!,PB=null!;public TableDto TA=null!,TB=null!;public ShiftDto SA=null!,SB=null!;public OrderDto OA=null!,OB=null!;
        public int UA=>A.Administrator.Id;public int UB=>B.Administrator.Id;public int DA=>A.Outlet.Id;public int DB=>B.Outlet.Id;
        public static async Task<Fixture> Create()
        {
            var f=new Fixture();
            f.Db.Roles.AddRange(new Role{Id=1,Name="Owner",Permissions="[]"},new Role{Id=2,Name="StoreManager",Permissions=JsonSerializer.Serialize(Enum.GetNames<Permission>().Where(p=>p!="ManageSettings"&&p!="ManagePeripherals"))});
            f.Db.Outlets.Add(new Outlet{Id=1,Name="Control",Currency="INR",Timezone="Asia/Kolkata",IsOperating=true});
            f.Db.Users.Add(new User{Id=1,Username="root",Email="root@example.invalid",RoleId=1,OutletId=1,IsActive=true});await f.Db.SaveChangesAsync();
            f.Auth=new(f.Db,f.Config);f.Users=new(f.Db,f.Auth);f.Outlets=new(f.Db);f.Menu=new(f.Db);f.Tables=new(f.Db);f.Inventory=new(f.Db);f.Shifts=new(f.Db);f.Reports=new(f.Db);f.Kitchen=new(f.Db);f.Orders=new(f.Db,f.Inventory,f.Kitchen);f.Provision=new(f.Db,f.Outlets,f.Users);
            f.A=await f.Provision.CreateAsync(Request("Tenant A","tenant_a_admin"),1);f.B=await f.Provision.CreateAsync(Request("Tenant B","tenant_b_admin"),1);
            var ca=await f.Menu.CreateCategoryAsync(new CreateCategoryRequest{OutletId=f.DA,Name="Menu A"},f.UA);var cb=await f.Menu.CreateCategoryAsync(new CreateCategoryRequest{OutletId=f.DB,Name="Menu B"},f.UB);
            f.PA=await f.Menu.CreateProductAsync(new CreateProductRequest{OutletId=f.DA,CategoryId=ca.Id,Name="Item A",SKU="SAME-SKU",Price=100,IsAvailable=true},f.UA);
            f.PB=await f.Menu.CreateProductAsync(new CreateProductRequest{OutletId=f.DB,CategoryId=cb.Id,Name="Item B",SKU="SAME-SKU",Price=200,IsAvailable=true},f.UB);
            f.TA=await f.Tables.CreateTableAsync(new CreateTableRequest{OutletId=f.DA,TableNumber="1",Capacity=4},f.UA);f.TB=await f.Tables.CreateTableAsync(new CreateTableRequest{OutletId=f.DB,TableNumber="1",Capacity=4},f.UB);
            await f.Inventory.RecordInventoryMovementAsync(new RecordInventoryRequest{ProductId=f.PA.Id,MovementType="Receipt",Quantity=100,Source="Tenant A stock"},f.UA);
            await f.Inventory.RecordInventoryMovementAsync(new RecordInventoryRequest{ProductId=f.PB.Id,MovementType="Receipt",Quantity=200,Source="Tenant B stock"},f.UB);
            f.SA=await f.Shifts.OpenShiftAsync(new OpenShiftRequest{OutletId=f.DA},f.UA);f.SB=await f.Shifts.OpenShiftAsync(new OpenShiftRequest{OutletId=f.DB},f.UB);
            f.OA=await f.Orders.CreateOrderAsync(new CreateOrderRequest{OutletId=f.DA,TableId=f.TA.Id,ServiceType="DineIn",Items=new(){new(){ProductId=f.PA.Id,Quantity=1}}},f.UA);
            f.OB=await f.Orders.CreateOrderAsync(new CreateOrderRequest{OutletId=f.DB,TableId=f.TB.Id,ServiceType="DineIn",Items=new(){new(){ProductId=f.PB.Id,Quantity=1}}},f.UB);
            await f.Orders.ProcessPaymentAsync(f.OA.Id,new ProcessPaymentRequest{TenderType="Cash",Amount=105},f.UA);await f.Orders.ProcessPaymentAsync(f.OB.Id,new ProcessPaymentRequest{TenderType="Cash",Amount=210},f.UB);
            return f;
        }
        public static CreateOutletWithAdminRequest Request(string name,string username)=>new(){Outlet=new OutletDto{Name=name,Currency="INR",Timezone="Asia/Kolkata",IsOperating=true},AdminUsername=username,AdminPassword="Tenant!SecurePassword123"};
        public void Dispose()=>Db.Dispose();
    }

    [Fact] public async Task ProvisioningCreatesSeparateLoginWithHashedPasswordAndNoSharedData()
    {
        using var f=await Fixture.Create();var login=await f.Auth.LoginAsync(new LoginRequest{Username="tenant_a_admin",Password="Tenant!SecurePassword123"});
        Assert.Equal(f.DA,login.User.OutletId);Assert.Equal("StoreManager",login.User.RoleName);
        var stored=await f.Db.Users.FindAsync(f.UA);Assert.NotEqual("Tenant!SecurePassword123",stored!.PasswordHash);Assert.True(BCrypt.Net.BCrypt.Verify("Tenant!SecurePassword123",stored.PasswordHash));
        Assert.DoesNotContain("Tenant!SecurePassword123",JsonSerializer.Serialize(f.A));Assert.DoesNotContain("Tenant!SecurePassword123",string.Join("",await f.Db.AuditLogs.Select(a=>a.AfterValues).ToListAsync()));
        var c=await f.Provision.CreateAsync(Fixture.Request("Empty new outlet","new_outlet_admin"),1);
        Assert.Empty(await f.Menu.GetProductsAsync(c.Outlet.Id,null,false,c.Administrator.Id));Assert.Empty((await f.Orders.GetOrdersAsync(new PagedRequest(),null,c.Administrator.Id)).Items);
        Assert.Single(await f.Users.GetUsersAsync(c.Administrator.Id));
    }
    [Fact] public async Task DuplicateAdminDoesNotCreateAnOrphanOutlet()
    {
        using var f=await Fixture.Create();var before=await f.Db.Outlets.CountAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Provision.CreateAsync(Fixture.Request("Should not exist","tenant_a_admin"),1));Assert.Equal(before,await f.Db.Outlets.CountAsync());
    }
    [Fact] public async Task OutletAdminsCreateIndependentCategoriesAndRejectOwnDuplicates()
    {
        using var f=await Fixture.Create();var a=await f.Menu.CreateCategoryAsync(new CreateCategoryRequest{OutletId=f.DA,Name="Fresh breads"},f.UA);var b=await f.Menu.CreateCategoryAsync(new CreateCategoryRequest{OutletId=f.DB,Name="Fresh breads"},f.UB);
        Assert.NotEqual(a.Id,b.Id);Assert.Equal(f.DA,a.OutletId);Assert.Equal(f.DB,b.OutletId);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Menu.CreateCategoryAsync(new CreateCategoryRequest{OutletId=f.DA,Name="  FRESH BREADS  "},f.UA));
    }
    [Fact] public async Task OnlySuperAdminCanProvisionOutletsOrGrantOwnerRole()
    {
        using var f=await Fixture.Create();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Provision.CreateAsync(Fixture.Request("Blocked","blocked_admin"),f.UA));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Outlets.CreateOutletAsync(new OutletDto{Name="Bypass",Currency="INR",Timezone="Asia/Kolkata"},f.UA));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Users.CreateUserAsync(new CreateUserRequest{Username="escalate",Email="escalate@example.invalid",Password="Test!Password123",RoleId=1,OutletId=f.DA},f.UA));
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task OmittedListScopeAlwaysMeansTheLoggedInOutlet(bool tenantB)
    {
        using var f=await Fixture.Create();var user=tenantB?f.UB:f.UA;var outlet=tenantB?f.DB:f.DA;var name=tenantB?"Item B":"Item A";var total=tenantB?210:105;
        Assert.Equal(outlet,Assert.Single(await f.Outlets.GetOutletsAsync(user)).Id);
        Assert.Equal(outlet,Assert.Single(await f.Users.GetUsersAsync(user)).OutletId);
        Assert.Equal(outlet,Assert.Single((await f.Orders.GetOrdersAsync(new PagedRequest(),null,user)).Items).OutletId);
        Assert.Equal(name,Assert.Single(await f.Inventory.GetStockLevelsAsync(null,user)).ProductName);
        Assert.All(await f.Inventory.GetInventoryMovementsAsync(null,new PagedRequest(),user),m=>Assert.Equal(name,m.ProductName));
        Assert.Equal(outlet,Assert.Single(await f.Shifts.GetShiftsAsync(null,new PagedRequest(),user)).OutletId);
        Assert.Equal(total,(await f.Reports.GetDashboardSummaryAsync(null,user)).TodaySales);
        Assert.Equal(total,Assert.Single(await f.Reports.GetSalesReportAsync(new SalesReportRequest(),user)).Cash);
        Assert.All(await f.Kitchen.GetKitchenTicketsAsync(null,null,user),t=>Assert.Contains(name,t.Items.Select(i=>i.ProductName)));
    }
    public static IEnumerable<object[]> CrossAccess => new[]{"outlet","products","categories","modifiers","tables","zones","stock","ledger","recipes","sales","dashboard","audit","shifts","zreport","order","ticket","staff","updateOutlet","createCategory","createProduct","updateProduct","createTable","tableStatus","createZone","recordStock","closeShift","void","discount","addItem","removeItem","payment","refund","transfer","merge","split","editStaff","deleteStaff","createStaff","image"}.Select(x=>new object[]{x});
    [Theory][MemberData(nameof(CrossAccess))] public async Task OtherOutletIdsCannotBeReadOrModified(string operation)
    {
        using var f=await Fixture.Create();var ticket=await f.Db.KitchenTickets.Where(t=>t.OrderId==f.OB.Id).Select(t=>t.Id).FirstAsync();
        Task attempt=operation switch {
            "outlet"=>f.Outlets.GetOutletByIdAsync(f.DB,f.UA),"products"=>f.Menu.GetProductsAsync(f.DB,null,false,f.UA),"categories"=>f.Menu.GetCategoriesAsync(f.DB,f.UA),"modifiers"=>f.Menu.GetModifierGroupsAsync(f.DB,f.UA),"tables"=>f.Tables.GetTablesAsync(f.DB,f.UA),"zones"=>f.Tables.GetZonesAsync(f.DB,f.UA),
            "stock"=>f.Inventory.GetStockLevelsAsync(f.DB,f.UA),"ledger"=>f.Inventory.GetInventoryMovementsAsync(f.DB,new PagedRequest(),f.UA),"recipes"=>f.Inventory.GetRecipesAsync(f.PB.Id,f.UA),"sales"=>f.Reports.GetSalesReportAsync(new SalesReportRequest{OutletId=f.DB},f.UA),"dashboard"=>f.Reports.GetDashboardSummaryAsync(f.DB,f.UA),"audit"=>f.Reports.GetAuditLogsAsync(f.DB,new PagedRequest(),f.UA),"shifts"=>f.Shifts.GetShiftsAsync(f.DB,new PagedRequest(),f.UA),"zreport"=>f.Shifts.GenerateZReportAsync(f.SB.Id,f.UA),"order"=>f.Orders.GetOrderByIdAsync(f.OB.Id,f.UA),"ticket"=>f.Kitchen.GetKitchenTicketByIdAsync(ticket,f.UA),"staff"=>f.Users.GetUserByIdAsync(f.UB,f.UA),
            "updateOutlet"=>f.Outlets.UpdateOutletAsync(f.DB,f.B.Outlet,f.UA),"createCategory"=>f.Menu.CreateCategoryAsync(new CreateCategoryRequest{OutletId=f.DB,Name="Cross"},f.UA),"createProduct"=>f.Menu.CreateProductAsync(new CreateProductRequest{OutletId=f.DB,Name="Cross",SKU="Cross",Price=1},f.UA),"updateProduct"=>f.Menu.UpdateProductAsync(f.PB.Id,new CreateProductRequest{OutletId=f.DB,Name="Cross",SKU="Cross",Price=1},f.UA),"createTable"=>f.Tables.CreateTableAsync(new CreateTableRequest{OutletId=f.DB,TableNumber="99",Capacity=4},f.UA),"tableStatus"=>f.Tables.UpdateTableStatusAsync(f.TB.Id,"Free",f.UA),"createZone"=>f.Tables.CreateZoneAsync(new CreateZoneRequest{OutletId=f.DB,Name="Cross"},f.UA),"recordStock"=>f.Inventory.RecordInventoryMovementAsync(new RecordInventoryRequest{ProductId=f.PB.Id,MovementType="Receipt",Quantity=10},f.UA),"closeShift"=>f.Shifts.CloseShiftAsync(new CloseShiftRequest{ShiftId=f.SB.Id,CountedCash=0},f.UA),
            "void"=>f.Orders.VoidOrderAsync(f.OB.Id,new VoidOrderRequest{Reason="Cross"},f.UA),"discount"=>f.Orders.ApplyDiscountAsync(f.OB.Id,new ApplyDiscountRequest{Percent=1,Reason="Cross"},f.UA),"addItem"=>f.Orders.AddOrderItemAsync(f.OB.Id,new OrderItemRequest{ProductId=f.PB.Id,Quantity=1},f.UA),"removeItem"=>f.Orders.RemoveOrderItemAsync(f.OB.Id,f.OB.OrderItems[0].Id,f.UA),"payment"=>f.Orders.ProcessPaymentAsync(f.OB.Id,new ProcessPaymentRequest{TenderType="Cash",Amount=210},f.UA),"refund"=>f.Orders.RefundAsync(f.OB.Id,new RefundRequest{Reason="Cross",Reference="QA"},f.UA),"transfer"=>f.Orders.TransferTableAsync(f.OB.Id,f.TB.Id,f.UA),"merge"=>f.Orders.MergeOrdersAsync(f.OB.Id,f.OA.Id,f.UA),"split"=>f.Orders.SplitOrderAsync(f.OB.Id,new(){f.OB.OrderItems[0].Id},f.TB.Id,f.UA),
            "editStaff"=>f.Users.UpdateUserAsync(f.UB,new UpdateUserRequest{Username="changed",Email="changed@example.invalid",RoleId=2,OutletId=f.DB,IsActive=true},f.UA),"deleteStaff"=>f.Users.DeleteUserAsync(f.UB,f.UA),"createStaff"=>f.Users.CreateUserAsync(new CreateUserRequest{Username="cross-user",Email="cross@example.invalid",Password="Valid!Password123",RoleId=2,OutletId=f.DB},f.UA),"image"=>new ProductImageService(f.Db).SaveAsync(f.PB.Id,new byte[30],f.UA),_=>throw new ArgumentOutOfRangeException(nameof(operation))};
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async()=>await attempt);
        Assert.Equal("Closed",(await f.Db.Orders.FindAsync(f.OB.Id))!.Status);Assert.Equal("Open",(await f.Db.Shifts.FindAsync(f.SB.Id))!.Status);Assert.True((await f.Db.Users.FindAsync(f.UB))!.IsActive);
        Assert.Equal(199m,Assert.Single(await f.Inventory.GetStockLevelsAsync(f.DB,f.UB)).CurrentStock);
    }
    [Fact] public async Task QrOrdersAndPreparationStatusBelongOnlyToTheSelectedOutlet()
    {
        using var f=await Fixture.Create();var a=new string('a',64);var b=new string('b',64);f.Db.DeviceConfigs.AddRange(new DeviceConfig{OutletId=f.DA,StationType="PublicOrdering",DeviceName="PublicOrdering-"+a,IsActive=true},new DeviceConfig{OutletId=f.DB,StationType="PublicOrdering",DeviceName="PublicOrdering-"+b,IsActive=true});await f.Db.SaveChangesAsync();
        var guest=new PublicOrderingService(f.Db,f.Orders,f.Menu,f.Config);Assert.Equal("Item A",Assert.Single((await guest.GetMenuAsync(a)).Products).Name);Assert.Equal("Item B",Assert.Single((await guest.GetMenuAsync(b)).Products).Name);
        var confirmation=await guest.PlaceAsync(a,new GuestOrderRequest{RequestId=Guid.NewGuid(),CustomerName="QA",CustomerPhone="9999999999",Items=new(){new(){ProductId=f.PA.Id,Quantity=1}}});
        var order=await f.Db.Orders.SingleAsync(o=>o.OrderNumber==confirmation.OrderNumber);Assert.Equal(f.DA,order.OutletId);Assert.DoesNotContain((await f.Orders.GetOrdersAsync(new PagedRequest(),null,f.UB)).Items,o=>o.Id==order.Id);
        var ticket=await f.Db.KitchenTickets.SingleAsync(t=>t.OrderId==order.Id);foreach(var status in new[]{"Accepted","InProgress","Ready"})await f.Kitchen.UpdateTicketStatusAsync(new UpdateTicketStatusRequest{TicketId=ticket.Id,Status=status},f.UA);
        Assert.Equal("Ready",(await guest.TrackAsync(a,confirmation.OrderNumber,confirmation.TrackingProof)).PreparationStatus);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>guest.TrackAsync(b,confirmation.OrderNumber,confirmation.TrackingProof));
    }
}
