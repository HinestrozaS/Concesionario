using System.Data;
using Concesionario.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Concesionario.Api.Repositories;

public interface IConcesionarioRepository
{
    Task<IEnumerable<FacturaDto>> ResumenVentasAsync();
    Task<IEnumerable<ResumenClienteDto>> ResumenPorClienteAsync();
    Task<IEnumerable<FacturaDto>> FacturasPorFechaAsync(DateTime desde, DateTime hasta);
    Task<decimal> DescuentoAsync(int idCliente, decimal monto);
    Task<int> RegistrarFacturaAsync(FacturaCrearDto dto);
    Task<ResultadoCursorDto> ProcesarPendientesAsync(decimal umbral);
    Task<int> ActualizarPreciosAsync(ActualizarPreciosDto dto);
    Task<IEnumerable<ClienteDto>> ClientesAsync();
    Task<int> RegistrarClienteAsync(ClienteCrearDto dto);
    Task<IEnumerable<AuditoriaDto>> AuditoriaAsync();
    Task<IEnumerable<LogErrorDto>> ErroresAsync();
    Task RegistrarUsuarioAsync(LoginDto dto);
    Task<bool> LoginAsync(LoginDto dto);
    Task<IEnumerable<CatalogoItemDto>?> CatalogoAsync(string tipo);
}

public class ConcesionarioRepository : IConcesionarioRepository
{
    private readonly string _cs;
    public ConcesionarioRepository(IConfiguration config) =>
        _cs = config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");

    private SqlConnection Db() => new(_cs);

    // Lista blanca de catálogos para los desplegables de la Web (la Web nunca manda SQL)
    private static readonly Dictionary<string, string> Catalogos = new()
    {
        ["ciudades"] = "SELECT idCiudad AS Id, NomCiudad AS Nombre FROM dbo.Ciudades ORDER BY NomCiudad",
        ["generos"] = "SELECT idGenero AS Id, NomGenero AS Nombre FROM dbo.Generos ORDER BY NomGenero",
        ["asesores"] = "SELECT A.idPersona AS Id, P.Nombre + ' ' + P.Apellidos AS Nombre FROM dbo.Asesores A JOIN dbo.Personas P ON P.idPersona = A.idPersona ORDER BY P.Nombre",
        ["tickets-sin-factura"] = @"SELECT T.idTicket AS Id, CONCAT('Ticket ', T.idTicket, ' - ', P.Nombre, ' ', P.Apellidos, ' - ', T.NumSerie) AS Nombre
                                   FROM dbo.Tickets T
                                   JOIN dbo.Personas P ON P.idPersona = T.idCliente
                                   LEFT JOIN dbo.Facturas F ON F.idTicket = T.idTicket
                                   WHERE F.idFactura IS NULL ORDER BY T.idTicket"
    };

    public async Task<IEnumerable<CatalogoItemDto>?> CatalogoAsync(string tipo)
    {
        if (!Catalogos.TryGetValue(tipo, out var sql)) return null;
        using var db = Db();
        return await db.QueryAsync<CatalogoItemDto>(sql);
    }

    // ---- Vistas (Jhonny) ----
    public async Task<IEnumerable<FacturaDto>> ResumenVentasAsync()
    {
        using var db = Db();
        return await db.QueryAsync<FacturaDto>(
            "SELECT idFactura, FechaVenta, Cliente, Asesor, NumSerie, TotalRepuestos, TotalManoObra, Total FROM dbo.vw_ResumenVentas ORDER BY idFactura");
    }

    public async Task<IEnumerable<ResumenClienteDto>> ResumenPorClienteAsync()
    {
        using var db = Db();
        // NOEXPAND obliga a leer el índice de la vista indexada en cualquier edición de SQL Server
        return await db.QueryAsync<ResumenClienteDto>(
            "SELECT idCliente, CantidadLineas, TotalRepuestos FROM dbo.vw_RepuestosPorClienteIndexada WITH (NOEXPAND) ORDER BY TotalRepuestos DESC");
    }

    // ---- Funciones (Jhonny) ----
    public async Task<IEnumerable<FacturaDto>> FacturasPorFechaAsync(DateTime desde, DateTime hasta)
    {
        using var db = Db();
        return await db.QueryAsync<FacturaDto>(
            "SELECT idFactura, FechaVenta, Cliente, Total FROM dbo.fn_ListarFacturasPorFecha(@desde, @hasta) ORDER BY FechaVenta",
            new { desde = desde.Date, hasta = hasta.Date });
    }

    public async Task<decimal> DescuentoAsync(int idCliente, decimal monto)
    {
        using var db = Db();
        return await db.ExecuteScalarAsync<decimal>(
            "SELECT dbo.fn_CalcularDescuentoCliente(@idCliente, @monto)", new { idCliente, monto });
    }

    // ---- Procedimientos y triggers (Samuel) ----
    public async Task<int> RegistrarFacturaAsync(FacturaCrearDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@idTicket", d.IdTicket);
        p.Add("@idAsesor", d.IdAsesor);
        p.Add("@FechaVenta", d.FechaVenta, DbType.Date);
        p.Add("@idFacturaGenerada", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_RegistrarFacturaConTransaccion", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@idFacturaGenerada");
    }

    public async Task<ResultadoCursorDto> ProcesarPendientesAsync(decimal umbral)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@Umbral", umbral, DbType.Decimal, precision: 18, scale: 2);
        p.Add("@Marcadas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        var filas = (await db.QueryAsync<TicketPendienteDto>("dbo.sp_ProcesarFacturasPendientesCursor", p,
            commandType: CommandType.StoredProcedure)).ToList();
        return new ResultadoCursorDto { Marcadas = p.Get<int>("@Marcadas"), Tickets = filas };
    }

    public async Task<int> ActualizarPreciosAsync(ActualizarPreciosDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@Filtro", d.Filtro);
        p.Add("@Porcentaje", d.Porcentaje, DbType.Decimal, precision: 5, scale: 2);
        p.Add("@FilasImpactadas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_ActualizarPreciosPorLote", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@FilasImpactadas");
    }

    public async Task<IEnumerable<ClienteDto>> ClientesAsync()
    {
        using var db = Db();
        return await db.QueryAsync<ClienteDto>(
            @"SELECT C.idPersona AS IdCliente, P.Nombre, P.Apellidos, P.Correo, P.Telefono, C.Direccion, Ci.NomCiudad AS Ciudad
              FROM dbo.Clientes C
              JOIN dbo.Personas P ON P.idPersona = C.idPersona
              LEFT JOIN dbo.Ciudades Ci ON Ci.idCiudad = C.idCiudad
              ORDER BY C.idPersona");
    }

    public async Task<int> RegistrarClienteAsync(ClienteCrearDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@Nombre", d.Nombre);
        p.Add("@Apellidos", d.Apellidos);
        p.Add("@FechaNacimiento", d.FechaNacimiento, DbType.Date);
        p.Add("@Telefono", d.Telefono);
        p.Add("@Correo", d.Correo);
        p.Add("@idGenero", d.IdGenero, DbType.Int32);
        p.Add("@Direccion", d.Direccion);
        p.Add("@idCiudad", d.IdCiudad, DbType.Int32);
        p.Add("@idClienteGenerado", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_RegistrarCliente", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@idClienteGenerado");
    }

    public async Task<IEnumerable<AuditoriaDto>> AuditoriaAsync()
    {
        using var db = Db();
        return await db.QueryAsync<AuditoriaDto>(
            "SELECT TOP 100 Id_Auditoria AS IdAuditoria, Accion, idFactura AS IdFactura, FechaRegistro, Usuario FROM dbo.AuditoriaFacturas ORDER BY Id_Auditoria DESC");
    }

    public async Task<IEnumerable<LogErrorDto>> ErroresAsync()
    {
        using var db = Db();
        return await db.QueryAsync<LogErrorDto>(
            "SELECT TOP 100 Id_Log AS IdLog, FechaError, ErrorNumber, ErrorMessage, ErrorProcedure, Usuario FROM dbo.LogErrores ORDER BY Id_Log DESC");
    }

    // ---- Seguridad (Samuel) ----
    public async Task RegistrarUsuarioAsync(LoginDto d)
    {
        using var db = Db();
        await db.ExecuteAsync("dbo.sp_RegistrarUsuario", new { User = d.Username, ClavePlana = d.Password },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> LoginAsync(LoginDto d)
    {
        using var db = Db();
        var valido = await db.ExecuteScalarAsync<int>("dbo.sp_ValidarUsuario",
            new { User = d.Username, ClavePlana = d.Password }, commandType: CommandType.StoredProcedure);
        return valido == 1;
    }
}