using HawalekNow.Domain.Shared.Geography;
using HawalekNow.Infrastructure.Shared.Geography;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NetTopologySuite.Geometries;

namespace HawalekNow.IntegrationTests;

public sealed class GeographyTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Fact]
    public void Conversion_uses_longitude_x_latitude_y_and_rejects_empty_wrong_srid_or_extra_dimensions()
    {
        var location = GeoPoint.Create(30.0444, 31.2357); var point = GeographyPointMapping.ToSqlPoint(location); Assert.Equal(31.2357, point.X); Assert.Equal(30.0444, point.Y); Assert.Equal(4326, point.SRID); Assert.Equal(location, GeographyPointMapping.FromSqlPoint(point));
        Assert.Throws<ArgumentException>(() => GeographyPointMapping.FromSqlPoint(new Point(1, 2) { SRID = 0 }));
        Assert.Throws<ArgumentException>(() => GeographyPointMapping.FromSqlPoint(new Point((Coordinate)null!) { SRID = 4326 }));
        Assert.Throws<ArgumentException>(() => GeographyPointMapping.FromSqlPoint(new GeometryFactory(new PrecisionModel(), 4326).CreatePoint()));
        Assert.Throws<ArgumentException>(() => GeographyPointMapping.FromSqlPoint(new Point(new CoordinateZ(1, 2, 3)) { SRID = 4326 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeographyPointMapping.FromSqlPoint(new Point(31, 91) { SRID = 4326 }));
    }
    [Fact]
    public async Task Same_production_context_roundtrips_real_sql_geography_and_measures_one_equatorial_degree_in_meters()
    {
        await using var normal = database.CreateContext(); Assert.Null(normal.Model.FindEntityType(typeof(SpatialVerificationRow)));
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).ReplaceService<IModelCustomizer, SpatialVerificationModel>().Options;
        await using var context = new HawalekDbContext(options);
        // Only this owned test database receives this table. No production migration/configuration knows the type.
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE [operations].[P03SpatialVerification] ([Id] uniqueidentifier NOT NULL PRIMARY KEY, [Place] geography NOT NULL CONSTRAINT CK_TestSpatial_Point CHECK ([Place].STSrid=4326 AND [Place].STGeometryType()='Point'))");
        var row = new SpatialVerificationRow { Id = Guid.NewGuid(), Place = GeoPoint.Create(30.0444, 31.2357) }; context.Add(row); await context.SaveChangesAsync(); context.ChangeTracker.Clear();
        Assert.Equal(row.Place, (await context.Set<SpatialVerificationRow>().SingleAsync(value => value.Id == row.Id)).Place);
        var metadata = await context.Database.SqlQueryRaw<string>("SELECT TYPE_NAME(user_type_id) AS [Value] FROM sys.columns WHERE object_id=OBJECT_ID('operations.P03SpatialVerification') AND name='Place'").SingleAsync(); Assert.Equal("geography", metadata);
        var coordinates = await context.Database.SqlQueryRaw<SpatialCoordinates>("SELECT [Place].Lat AS Latitude,[Place].Long AS Longitude,[Place].STSrid AS Srid FROM [operations].[P03SpatialVerification]").SingleAsync(); Assert.Equal(30.0444, coordinates.Latitude, 7); Assert.Equal(31.2357, coordinates.Longitude, 7); Assert.Equal(4326, coordinates.Srid);
        context.AddRange(new SpatialVerificationRow { Id = Guid.NewGuid(), Place = GeoPoint.Create(0, 0) }, new SpatialVerificationRow { Id = Guid.NewGuid(), Place = GeoPoint.Create(0, 1) }); await context.SaveChangesAsync();
        var meters = await context.Database.SqlQueryRaw<double>("SELECT a.Place.STDistance(b.Place) AS [Value] FROM [operations].[P03SpatialVerification] a CROSS JOIN [operations].[P03SpatialVerification] b WHERE a.Place.Lat=0 AND a.Place.Long=0 AND b.Place.Lat=0 AND b.Place.Long=1").SingleAsync(); Assert.InRange(meters, 111319.49 - 300, 111319.49 + 300);
        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync("INSERT INTO [operations].[P03SpatialVerification] VALUES (NEWID(),geography::Point(30,31,4269))"));
        Assert.Null(normal.Model.FindEntityType(typeof(SpatialVerificationRow))); Assert.False(normal.Database.HasPendingModelChanges());
    }
}
public sealed class SpatialVerificationRow { public Guid Id { get; set; } public GeoPoint Place { get; set; } }
public sealed class SpatialCoordinates { public double Latitude { get; set; } public double Longitude { get; set; } public int Srid { get; set; } }
public sealed class SpatialVerificationModel(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    { base.Customize(modelBuilder, context); var row = modelBuilder.Entity<SpatialVerificationRow>(); row.ToTable("P03SpatialVerification", "operations"); row.HasKey(value => value.Id); row.Property(value => value.Place).HasGeographyPoint(); }
}
