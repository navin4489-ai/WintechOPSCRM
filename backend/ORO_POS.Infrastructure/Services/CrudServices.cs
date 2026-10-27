using Microsoft.EntityFrameworkCore;
using ORO_POS.Core.DTOs;
using ORO_POS.Core.Entities;
using ORO_POS.Core.Interfaces;
using ORO_POS.Infrastructure.Data;

namespace ORO_POS.Infrastructure.Services;

public class OutletService : IOutletService
{
    private readonly AppDbContext _db;

    public OutletService(AppDbContext db) => _db = db;

    public async Task<List<OutletDto>> GetOutletsAsync(int userId)
    {
        var outlets = await _db.Outlets.Where(o => !o.IsDeleted).ToListAsync();
        return outlets.Select(o => new OutletDto
        {
            Id = o.Id, Name = o.Name, Address = o.Address, Phone = o.Phone,
            Timezone = o.Timezone, TaxSetup = o.TaxSetup, Currency = o.Currency, IsOperating = o.IsOperating
        }).ToList();
    }

    public async Task<OutletDto?> GetOutletByIdAsync(int id, int userId)
    {
        var o = await _db.Outlets.FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
        return o == null ? null : new OutletDto
        {
            Id = o.Id, Name = o.Name, Address = o.Address, Phone = o.Phone,
            Timezone = o.Timezone, TaxSetup = o.TaxSetup, Currency = o.Currency, IsOperating = o.IsOperating
        };
    }

    public async Task<OutletDto> CreateOutletAsync(OutletDto dto, int userId)
    {
        var outlet = new Outlet
        {
            Name = dto.Name, Address = dto.Address, Phone = dto.Phone,
            Timezone = dto.Timezone, TaxSetup = dto.TaxSetup, Currency = dto.Currency, IsOperating = dto.IsOperating,
            CreatedAt = DateTime.UtcNow, CreatedBy = (await _db.Users.FindAsync(userId))?.Username
        };
        _db.Outlets.Add(outlet);
        await _db.SaveChangesAsync();
        dto.Id = outlet.Id;
        return dto;
    }

    public async Task<OutletDto> UpdateOutletAsync(int id, OutletDto dto, int userId)
    {
        var outlet = await _db.Outlets.FindAsync(id);
        if (outlet == null) throw new KeyNotFoundException("Outlet not found.");
        outlet.Name = dto.Name; outlet.Address = dto.Address; outlet.Phone = dto.Phone;
        outlet.Timezone = dto.Timezone; outlet.TaxSetup = dto.TaxSetup; outlet.Currency = dto.Currency;
        outlet.IsOperating = dto.IsOperating; outlet.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return dto;
    }
}

public class MenuService : IMenuService
{
    private readonly AppDbContext _db;

    public MenuService(AppDbContext db) => _db = db;

    public async Task<List<CategoryDto>> GetCategoriesAsync(int outletId, int userId)
    {
        return await _db.Categories.Where(c => c.OutletId == outletId && !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto { Id = c.Id, OutletId = c.OutletId, Name = c.Name, SortOrder = c.SortOrder })
            .ToListAsync();
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, int userId)
    {
        var cat = new Category { OutletId = request.OutletId, Name = request.Name, SortOrder = request.SortOrder };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return new CategoryDto { Id = cat.Id, OutletId = cat.OutletId, Name = cat.Name, SortOrder = cat.SortOrder };
    }

    public async Task<List<ProductDto>> GetProductsAsync(int outletId, int? categoryId, bool availableOnly, int userId)
    {
        var query = _db.Products.Include(p => p.Category).Include(p => p.ModifierGroups).ThenInclude(pmg => pmg.ModifierGroup).ThenInclude(mg => mg.Options)
            .Where(p => p.OutletId == outletId && !p.IsDeleted);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (availableOnly) query = query.Where(p => p.IsAvailable);

        var products = await query.ToListAsync();
        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, int userId)
    {
        var product = new Product
        {
            OutletId = request.OutletId, CategoryId = request.CategoryId, SKU = request.SKU,
            Barcode = request.Barcode, Name = request.Name, Description = request.Description,
            Price = request.Price, TaxClass = request.TaxClass, IsAvailable = request.IsAvailable,
            SortOrder = request.SortOrder, CreatedAt = DateTime.UtcNow
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        foreach (var mgId in request.ModifierGroupIds)
        {
            _db.ProductModifierGroups.Add(new ProductModifierGroup { ProductId = product.Id, ModifierGroupId = mgId });
        }
        await _db.SaveChangesAsync();

        return await GetProductDtoAsync(product.Id);
    }

    public async Task<List<ModifierGroupDto>> GetModifierGroupsAsync(int outletId, int userId)
    {
        return await _db.ModifierGroups.Include(mg => mg.Options).Where(mg => mg.OutletId == outletId && !mg.IsDeleted)
            .Select(mg => new ModifierGroupDto
            {
                Id = mg.Id, OutletId = mg.OutletId, Name = mg.Name, IsRequired = mg.IsRequired,
                MinSelections = mg.MinSelections, MaxSelections = mg.MaxSelections,
                Options = mg.Options.Select(o => new ModifierOptionDto { Id = o.Id, ModifierGroupId = o.ModifierGroupId, Name = o.Name, PriceImpact = o.PriceImpact }).ToList()
            }).ToListAsync();
    }

    public async Task<ModifierGroupDto> CreateModifierGroupAsync(CreateModifierGroupRequest request, int userId)
    {
        var mg = new ModifierGroup
        {
            OutletId = request.OutletId, Name = request.Name, IsRequired = request.IsRequired,
            MinSelections = request.MinSelections, MaxSelections = request.MaxSelections
        };
        _db.ModifierGroups.Add(mg);
        await _db.SaveChangesAsync();

        foreach (var opt in request.Options)
        {
            _db.ModifierOptions.Add(new ModifierOption { ModifierGroupId = mg.Id, Name = opt.Name, PriceImpact = opt.PriceImpact });
        }
        await _db.SaveChangesAsync();

        return new ModifierGroupDto
        {
            Id = mg.Id, OutletId = mg.OutletId, Name = mg.Name, IsRequired = mg.IsRequired,
            MinSelections = mg.MinSelections, MaxSelections = mg.MaxSelections,
            Options = request.Options.Select((o, i) => new ModifierOptionDto { Id = i + 1, ModifierGroupId = mg.Id, Name = o.Name, PriceImpact = o.PriceImpact }).ToList()
        };
    }

    private async Task<ProductDto> GetProductDtoAsync(int productId)
    {
        var p = await _db.Products.Include(x => x.Category).Include(x => x.ModifierGroups).ThenInclude(pmg => pmg.ModifierGroup).ThenInclude(mg => mg.Options)
            .FirstAsync(x => x.Id == productId);
        return MapToDto(p);
    }

    private static ProductDto MapToDto(Product p)
    {
        return new ProductDto
        {
            Id = p.Id, OutletId = p.OutletId, CategoryId = p.CategoryId, CategoryName = p.Category?.Name,
            SKU = p.SKU, Barcode = p.Barcode, Name = p.Name, Description = p.Description,
            Price = p.Price, TaxClass = p.TaxClass, IsAvailable = p.IsAvailable,
            SortOrder = p.SortOrder, ImageUrl = p.ImageUrl,
            ModifierGroups = p.ModifierGroups.Select(pmg => new ModifierGroupDto
            {
                Id = pmg.ModifierGroup.Id, Name = pmg.ModifierGroup.Name,
                Options = pmg.ModifierGroup.Options.Select(o => new ModifierOptionDto { Id = o.Id, Name = o.Name, PriceImpact = o.PriceImpact }).ToList()
            }).ToList()
        };
    }
}

public class TableService : ITableService
{
    private readonly AppDbContext _db;

    public TableService(AppDbContext db) => _db = db;

    public async Task<List<TableDto>> GetTablesAsync(int outletId, int userId)
    {
        var tables = await _db.Tables.Include(t => t.Zone).Where(t => t.OutletId == outletId && !t.IsDeleted).ToListAsync();
        var openOrderIds = await _db.Orders.Where(o => o.Status != "Closed" && o.Status != "Voided").Select(o => o.TableId).ToListAsync();

        return tables.Select(t => new TableDto
        {
            Id = t.Id, OutletId = t.OutletId, ZoneId = t.ZoneId, ZoneName = t.Zone?.Name,
            TableNumber = t.TableNumber, Status = t.Status, Capacity = t.Capacity,
            FloorPlanX = t.FloorPlanX, FloorPlanY = t.FloorPlanY,
            HasOpenOrder = openOrderIds.Contains(t.Id)
        }).ToList();
    }

    public async Task<TableDto> CreateTableAsync(CreateTableRequest request, int userId)
    {
        var table = new Table
        {
            OutletId = request.OutletId, ZoneId = request.ZoneId, TableNumber = request.TableNumber,
            Status = "Free", Capacity = request.Capacity, FloorPlanX = request.FloorPlanX, FloorPlanY = request.FloorPlanY
        };
        _db.Tables.Add(table);
        await _db.SaveChangesAsync();
        return new TableDto { Id = table.Id, OutletId = table.OutletId, TableNumber = table.TableNumber, Status = table.Status, Capacity = table.Capacity };
    }

    public async Task<TableDto> UpdateTableStatusAsync(int tableId, string status, int userId)
    {
        var table = await _db.Tables.FindAsync(tableId);
        if (table == null) throw new KeyNotFoundException("Table not found.");
        table.Status = status; table.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new TableDto { Id = table.Id, TableNumber = table.TableNumber, Status = table.Status };
    }

    public async Task<List<ZoneDto>> GetZonesAsync(int outletId, int userId)
    {
        return await _db.Zones.Where(z => z.OutletId == outletId && !z.IsDeleted)
            .Select(z => new ZoneDto { Id = z.Id, OutletId = z.OutletId, Name = z.Name, FloorPlanData = z.FloorPlanData })
            .ToListAsync();
    }

    public async Task<ZoneDto> CreateZoneAsync(CreateZoneRequest request, int userId)
    {
        var zone = new Zone { OutletId = request.OutletId, Name = request.Name, FloorPlanData = request.FloorPlanData };
        _db.Zones.Add(zone);
        await _db.SaveChangesAsync();
        return new ZoneDto { Id = zone.Id, OutletId = zone.OutletId, Name = zone.Name, FloorPlanData = zone.FloorPlanData };
    }
}
