using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Infrastructure.Data;
using ORO_POS.Infrastructure.Services;

namespace ORO_POS.Tests;
public class PublicOrderingTests : IDisposable
{
    private readonly AppDbContext db;
    private readonly OrderService orders;
    private readonly PublicOrderingService guest;
    private const string Key = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    public PublicOrderingTests()
    {
        db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Outlets.Add(new Outlet { Id=1, Name="QR outlet", Currency="INR", IsOperating=true });
        db.Roles.Add(new Role { Id=1, Name="Owner", Permissions="[]" });
        db.Users.Add(new User { Id=1, Username="owner", RoleId=1, OutletId=1, IsActive=true });
        db.Shifts.Add(new Shift { Id=1, UserId=1, OutletId=1, Status="Open" });
        db.Products.Add(new Product { Id=1, OutletId=1, Name="Meal", SKU="P1", Price=100, IsAvailable=true });
        db.DeviceConfigs.Add(new DeviceConfig { DeviceName="PublicOrdering-"+Key, OutletId=1, StationType="PublicOrdering", IsActive=true });
        db.SaveChanges();
        orders = new(db,new InventoryService(db),new KitchenService(db));
        guest = new(db,orders,new MenuService(db),new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Jwt:Key"]="test-only-signing-key-with-at-least-32-bytes"}).Build());
    }
    private static GuestOrderRequest Request() => new() { RequestId=Guid.NewGuid(),CustomerName="QA guest",CustomerPhone="+91 99999 99999",Items=new(){new OrderItemRequest{ProductId=1,Quantity=2,ModifierOptionIds=new()}} };
    [Fact] public async Task MenuIsAnonymousSafeAndIncludesAvailability()
    { var menu=await guest.GetMenuAsync(Key);Assert.Equal("QR outlet",menu.OutletName);Assert.True(menu.AcceptingOrders);Assert.Single(menu.Products); }
    [Fact] public async Task GuestOrderUsesServerPricesRemainsUnpaidAndRetryIsIdempotent()
    {
        var request=Request();var first=await guest.PlaceAsync(Key,request);var second=await guest.PlaceAsync(Key,request);
        Assert.Equal(first.OrderNumber,second.OrderNumber);Assert.Equal(200,first.Subtotal);Assert.Equal(10,first.TaxAmount);Assert.Equal(210,first.TotalAmount);
        Assert.Equal("Open",first.Status);Assert.Empty(await db.Payments.ToListAsync());Assert.Single(await db.Orders.ToListAsync());
        Assert.Contains("CASH AT OUTLET",(await db.Orders.SingleAsync()).Notes);Assert.Single(await db.KitchenTickets.ToListAsync());
        Assert.Contains("Contact: +919999999999",(await db.Orders.SingleAsync()).Notes);
        Assert.DoesNotContain("9999999999",System.Text.Json.JsonSerializer.Serialize(first));
    }
    [Fact] public async Task ClosedOutletAndMissingShiftDoNotCreateOrders()
    {
        (await db.Outlets.FindAsync(1))!.IsOperating=false;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>guest.PlaceAsync(Key,Request()));
        (await db.Outlets.FindAsync(1))!.IsOperating=true;(await db.Shifts.FindAsync(1))!.Status="Closed";await db.SaveChangesAsync();
        Assert.False((await guest.GetMenuAsync(Key)).AcceptingOrders);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>guest.PlaceAsync(Key,Request()));Assert.Empty(await db.Orders.ToListAsync());
    }
    [Fact] public async Task DisabledAndRotatedQrLinksStopWorking()
    {
        var device=await db.DeviceConfigs.SingleAsync();device.IsActive=false;await db.SaveChangesAsync();await Assert.ThrowsAsync<KeyNotFoundException>(()=>guest.GetMenuAsync(Key));
        device.IsActive=true;device.DeviceName="PublicOrdering-"+new string('b',64);await db.SaveChangesAsync();await Assert.ThrowsAsync<KeyNotFoundException>(()=>guest.GetMenuAsync(Key));
        Assert.Equal("QR outlet",(await guest.GetMenuAsync(new string('b',64))).OutletName);
    }
    [Fact] public async Task TrackingRequiresProofAndCashIsCollectedByAuthenticatedStaff()
    {
        var confirmation=await guest.PlaceAsync(Key,Request());await Assert.ThrowsAsync<KeyNotFoundException>(()=>guest.TrackAsync(Key,confirmation.OrderNumber,"wrong"));
        var id=(await db.Orders.SingleAsync()).Id;
        await orders.ProcessPaymentAsync(id,new ProcessPaymentRequest{TenderType="Cash",Amount=210},1);
        var status=await guest.TrackAsync(Key,confirmation.OrderNumber,confirmation.TrackingProof);Assert.Equal("Paid at outlet",status.PaymentStatus);
    }
    [Fact] public async Task ProductImagesArePersistedReplacedAndExposedByUrlOnly()
    {
        var bytes=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC");
        var images=new ProductImageService(db);var url=await images.SaveAsync(1,bytes,1);Assert.StartsWith("/api/products/1/image?v=",url);
        Assert.Equal(url,(await guest.GetMenuAsync(Key)).Products.Single().ImageUrl);
        await images.SaveAsync(1,bytes,1);Assert.Single(await db.ProductImages.ToListAsync());
        await images.RemoveAsync(1,1);Assert.Empty(await db.ProductImages.ToListAsync());Assert.Null((await db.Products.FindAsync(1))!.ImageUrl);
    }
    [Fact] public void UnsafeImageFormatsAndOversizeFilesAreRejected()
    {Assert.Throws<InvalidOperationException>(()=>ProductImageService.Validate(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'></svg>")));Assert.Throws<InvalidOperationException>(()=>ProductImageService.Validate(new byte[ProductImageService.MaximumBytes+1]));}
    [Fact] public async Task OutletAdministratorCannotUploadAnotherOutletsImage()
    {
        db.Outlets.Add(new Outlet{Id=2,Name="Other"});db.Roles.Add(new Role{Id=2,Name="StoreManager",Permissions="[\"ManageSettings\"]"});db.Users.Add(new User{Id=2,Username="other-manager",RoleId=2,OutletId=2,IsActive=true});await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new ProductImageService(db).SaveAsync(1,new byte[30],2));Assert.Empty(await db.ProductImages.ToListAsync());
    }
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    public async Task GuestContactMustBeValid(string phone)
    { var request=Request();request.CustomerPhone=phone;await Assert.ThrowsAsync<InvalidOperationException>(()=>guest.PlaceAsync(Key,request));Assert.Empty(await db.Orders.ToListAsync()); }
    [Fact] public async Task OutletManagerCanUploadOwnItemWithoutPlatformSettingsPermission()
    {
        db.Roles.Add(new Role{Id=2,Name="StoreManager",Permissions="[]"});db.Users.Add(new User{Id=2,Username="outlet-admin",RoleId=2,OutletId=1,IsActive=true});await db.SaveChangesAsync();
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4//8/AAX+Av4N70a4AAAAAElFTkSuQmCC");
        Assert.StartsWith("/api/products/1/image",await new ProductImageService(db).SaveAsync(1,png,2));
    }
    [Fact] public async Task ExistingCustomerCanTrackWhenNewOrderingIsDisabled()
    {
        var result=await guest.PlaceAsync(Key,Request());(await db.DeviceConfigs.SingleAsync()).IsActive=false;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>guest.GetMenuAsync(Key));
        Assert.Equal(result.OrderNumber,(await guest.TrackAsync(Key,result.OrderNumber,result.TrackingProof)).OrderNumber);
    }
    public void Dispose()=>db.Dispose();
}
