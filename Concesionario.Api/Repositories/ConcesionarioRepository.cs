using System.Data;
using Concesionario.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Concesionario.Api.Repositories;

public interface IConcesionarioRepository
{
    // Vistas
    Task<IEnumerable<FacturaResumenDto>> FacturasAsync();
    Task<IEnumerable<IngresoServicioDto>> IngresosPorServicioAsync();

    // Funciones
    Task<TotalFacturaDto> TotalFacturaAsync(int idFactura);
    Task<IEnumerable<TrabajoDetalleDto>> TrabajosPorTicketAsync(int idTicket);

    // Procedimientos almacenados
    Task<int> CrearTicketAsync(TicketCrearDto dto);
    Task<int> AgregarTrabajoAsync(int idTicket, TrabajoCrearDto dto);
    Task<int> FacturarTicketAsync(FacturaCrearDto dto);
    Task<int> AjustarTarifasAsync(AjusteTarifasDto dto);
    Task<ResultadoCursorDto> FacturarPendientesAsync(int idAsesor);

    // Listados para las pantallas
    Task<IEnumerable<ClienteDto>> ClientesAsync();
    Task<IEnumerable<VehiculoDto>> VehiculosAsync();
    Task<IEnumerable<TicketDto>> TicketsAsync();
    Task<IEnumerable<AsesorDto>> AsesoresAsync();
    Task<IEnumerable<ServicioDto>> ServiciosAsync();

    // Evidencia de triggers y TRY-CATCH
    Task<IEnumerable<AuditoriaDto>> AuditoriaAsync();
    Task<IEnumerable<LogErrorDto>> ErroresAsync();

    // Seguridad
    Task RegistrarUsuarioAsync(LoginDto dto);
    Task<bool> LoginAsync(LoginDto dto);
}

public class ConcesionarioRepository : IConcesionarioRepository
{
    private readonly string _cs;

    public ConcesionarioRepository(IConfiguration config) =>
        _cs = config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");

    private SqlConnection Db() => new(_cs);

    // ------------------------------------------------------------------
    // VISTAS (03_Vistas_Funciones.sql)
    // ------------------------------------------------------------------
    public async Task<IEnumerable<FacturaResumenDto>> FacturasAsync()
    {
        using var db = Db();
        return await db.QueryAsync<FacturaResumenDto>(
            @"SELECT idFactura AS IdFactura, FechaVenta, Cliente, Asesor, idTicket AS IdTicket,
                     Marca, Modelo, NumSerie, TotalTrabajos, TotalRepuestos, Total
              FROM dbo.vw_ResumenFacturas
              ORDER BY idFactura");
    }

    public async Task<IEnumerable<IngresoServicioDto>> IngresosPorServicioAsync()
    {
        using var db = Db();
        // NOEXPAND obliga a leer el índice de la vista indexada en cualquier edición de SQL Server.
        // COUNT_BIG devuelve bigint: se convierte a INT para el DTO.
        return await db.QueryAsync<IngresoServicioDto>(
            @"SELECT NomServicio,
                     CAST(CantidadTrabajos AS INT) AS CantidadTrabajos,
                     HorasTotales,
                     IngresosTotales
              FROM dbo.vw_IngresosPorServicio WITH (NOEXPAND)
              ORDER BY NomServicio");
    }

    // ------------------------------------------------------------------
    // FUNCIONES (03_Vistas_Funciones.sql)
    // ------------------------------------------------------------------
    public async Task<TotalFacturaDto> TotalFacturaAsync(int idFactura)
    {
        using var db = Db();
        return await db.QuerySingleAsync<TotalFacturaDto>(
            "SELECT @idFactura AS IdFactura, dbo.fn_TotalFactura(@idFactura) AS Total",
            new { idFactura });
    }

    public async Task<IEnumerable<TrabajoDetalleDto>> TrabajosPorTicketAsync(int idTicket)
    {
        using var db = Db();
        return await db.QueryAsync<TrabajoDetalleDto>(
            @"SELECT Servicio, Descripcion, HorasTrabajadas, TarifaHora, Subtotal
              FROM dbo.fn_TrabajosPorTicket(@idTicket)",
            new { idTicket });
    }

    // ------------------------------------------------------------------
    // PROCEDIMIENTOS ALMACENADOS (04_Soporte_SP_Triggers.sql)
    // ------------------------------------------------------------------

    // Dispara trg_Tickets_UnicoAbierto (INSTEAD OF INSERT)
    public async Task<int> CrearTicketAsync(TicketCrearDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@idCliente", d.IdCliente);
        p.Add("@NumSerie", d.NumSerie);
        p.Add("@idTicketGenerado", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_RegistrarTicket", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@idTicketGenerado");
    }

    public async Task<int> AgregarTrabajoAsync(int idTicket, TrabajoCrearDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@idTicket", idTicket);
        p.Add("@idServicio", d.IdServicio);
        p.Add("@HorasTrabajadas", d.HorasTrabajadas, DbType.Decimal, precision: 4, scale: 2);
        p.Add("@Descripcion", d.Descripcion);
        p.Add("@idTrabajoGenerado", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_AgregarTrabajo", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@idTrabajoGenerado");
    }

    // Dispara trg_AuditoriaFacturas y trg_Facturas_CerrarTicketCursor
    public async Task<int> FacturarTicketAsync(FacturaCrearDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@idTicket", d.IdTicket);
        p.Add("@idAsesor", d.IdAsesor);
        p.Add("@idFacturaGenerada", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_RegistrarFacturaTicket", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@idFacturaGenerada");
    }

    public async Task<int> AjustarTarifasAsync(AjusteTarifasDto d)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@Porcentaje", d.Porcentaje, DbType.Decimal, precision: 5, scale: 2);
        p.Add("@idServicio", d.IdServicio, DbType.Int32);
        p.Add("@FilasImpactadas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        await db.ExecuteAsync("dbo.sp_AjustarTarifasServicios", p, commandType: CommandType.StoredProcedure);
        return p.Get<int>("@FilasImpactadas");
    }

    // Procedimiento con CURSOR + TRY...CATCH. Devuelve las facturas creadas (SELECT de la vista)
    public async Task<ResultadoCursorDto> FacturarPendientesAsync(int idAsesor)
    {
        using var db = Db();
        var p = new DynamicParameters();
        p.Add("@idAsesor", idAsesor);
        p.Add("@Facturadas", dbType: DbType.Int32, direction: ParameterDirection.Output);
        // Se enumera todo el resultado ANTES de leer el parámetro OUTPUT
        var facturas = (await db.QueryAsync<FacturaCursorDto>(
            "dbo.sp_FacturarTicketsPendientesCursor", p, commandType: CommandType.StoredProcedure)).ToList();
        return new ResultadoCursorDto
        {
            Facturadas = p.Get<int>("@Facturadas"),
            Facturas = facturas
        };
    }

    // ------------------------------------------------------------------
    // LISTADOS PARA LAS PANTALLAS
    // ------------------------------------------------------------------
    public async Task<IEnumerable<ClienteDto>> ClientesAsync()
    {
        using var db = Db();
        return await db.QueryAsync<ClienteDto>(
            @"SELECT C.idPersona AS IdCliente, P.Nombre, P.Apellidos, P.Correo, P.Telefono,
                     C.Direccion, Ci.NomCiudad AS Ciudad
              FROM dbo.Clientes C
              JOIN dbo.Personas P ON P.idPersona = C.idPersona
              LEFT JOIN dbo.Ciudades Ci ON Ci.idCiudad = C.idCiudad
              ORDER BY C.idPersona");
    }

    public async Task<IEnumerable<VehiculoDto>> VehiculosAsync()
    {
        using var db = Db();
        return await db.QueryAsync<VehiculoDto>(
            @"SELECT V.NumSerie, Ma.NomMarca AS Marca, Mo.NomModelo AS Modelo
              FROM dbo.Vehiculos V
              JOIN dbo.Marcas Ma ON Ma.idMarca = V.idMarca
              JOIN dbo.Modelos Mo ON Mo.idModelo = V.idModelo
              ORDER BY Ma.NomMarca, Mo.NomModelo, V.NumSerie");
    }

    public async Task<IEnumerable<TicketDto>> TicketsAsync()
    {
        using var db = Db();
        return await db.QueryAsync<TicketDto>(
            @"SELECT T.idTicket AS IdTicket,
                     P.Nombre + ' ' + P.Apellidos AS Cliente,
                     T.NumSerie,
                     CAST(CASE WHEN F.idFactura IS NULL THEN 0 ELSE 1 END AS BIT) AS Facturado
              FROM dbo.Tickets T
              JOIN dbo.Personas P ON P.idPersona = T.idCliente
              LEFT JOIN dbo.Facturas F ON F.idTicket = T.idTicket
              ORDER BY T.idTicket DESC");
    }

    public async Task<IEnumerable<AsesorDto>> AsesoresAsync()
    {
        using var db = Db();
        return await db.QueryAsync<AsesorDto>(
            @"SELECT A.idPersona AS IdAsesor, P.Nombre + ' ' + P.Apellidos AS NombreCompleto
              FROM dbo.Asesores A
              JOIN dbo.Personas P ON P.idPersona = A.idPersona
              ORDER BY P.Nombre, P.Apellidos");
    }

    public async Task<IEnumerable<ServicioDto>> ServiciosAsync()
    {
        using var db = Db();
        return await db.QueryAsync<ServicioDto>(
            "SELECT idServicio AS IdServicio, NomServicio FROM dbo.Servicios ORDER BY NomServicio");
    }

    // ------------------------------------------------------------------
    // EVIDENCIA DE TRIGGERS Y TRY...CATCH
    // ------------------------------------------------------------------
    public async Task<IEnumerable<AuditoriaDto>> AuditoriaAsync()
    {
        using var db = Db();
        return await db.QueryAsync<AuditoriaDto>(
            @"SELECT TOP 100 IdAuditoria, Accion, idFactura AS IdFactura, FechaRegistro, Usuario
              FROM dbo.AuditoriaFacturas
              ORDER BY IdAuditoria DESC");
    }

    public async Task<IEnumerable<LogErrorDto>> ErroresAsync()
    {
        using var db = Db();
        return await db.QueryAsync<LogErrorDto>(
            @"SELECT TOP 100 IdLog, FechaError, ErrorNumber, ErrorMessage, ErrorProcedure, Usuario
              FROM dbo.LogErrores
              ORDER BY IdLog DESC");
    }

    // ------------------------------------------------------------------
    // SEGURIDAD (05_Seguridad.sql): hash SHA2_256 con sal
    // ------------------------------------------------------------------
    public async Task RegistrarUsuarioAsync(LoginDto d)
    {
        using var db = Db();
        await db.ExecuteAsync("dbo.sp_RegistrarUsuario",
            new { User = d.Username, ClavePlana = d.Password },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> LoginAsync(LoginDto d)
    {
        using var db = Db();
        var valido = await db.ExecuteScalarAsync<int>("dbo.sp_ValidarUsuario",
            new { User = d.Username, ClavePlana = d.Password },
            commandType: CommandType.StoredProcedure);
        return valido == 1;
    }
}