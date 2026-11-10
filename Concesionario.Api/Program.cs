using Concesionario.Api.Repositories;
using Concesionario.Contracts;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// CORS no hace falta con Blazor Server (la llamada va de servidor a servidor); queda por si se prueba desde otro origen
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5200").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddScoped<IConcesionarioRepository, ConcesionarioRepository>();
builder.Services.AddScoped<IConsultasRepository, ConsultasRepository>(); // lo escribe Esteban

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();

// Si SQL Server lanza un error (THROW de un SP o de un trigger) se devuelve 400 con el mensaje,
// para que la Web pueda mostrarlo en pantalla.
static async Task<IResult> Ejecutar(Func<Task<IResult>> accion)
{
    try { return await accion(); }
    catch (SqlException ex) { return Results.BadRequest(new ApiError { Error = ex.Message, Numero = ex.Number }); }
}

var api = app.MapGroup("/api");

// ---------------- Vistas ----------------
api.MapGet("/vistas/facturas", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.FacturasAsync())));
api.MapGet("/vistas/ingresos-servicio", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.IngresosPorServicioAsync())));

// ---------------- Funciones ----------------
api.MapGet("/funciones/total-factura/{idFactura:int}", (int idFactura, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.TotalFacturaAsync(idFactura))));
api.MapGet("/funciones/trabajos-ticket/{idTicket:int}", (int idTicket, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.TrabajosPorTicketAsync(idTicket))));

// ---------------- Procedimientos almacenados ----------------
api.MapPost("/tickets", (TicketCrearDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new IdResultDto { Id = await r.CrearTicketAsync(dto) })));
api.MapPost("/tickets/{idTicket:int}/trabajos", (int idTicket, TrabajoCrearDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new IdResultDto { Id = await r.AgregarTrabajoAsync(idTicket, dto) })));
api.MapPost("/facturas", (FacturaCrearDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new IdResultDto { Id = await r.FacturarTicketAsync(dto) })));
api.MapPost("/servicios/ajustar-tarifas", (AjusteTarifasDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new FilasDto { Filas = await r.AjustarTarifasAsync(dto) })));
api.MapPost("/facturas/facturar-pendientes", (FacturarPendientesRequest req, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.FacturarPendientesAsync(req.IdAsesor))));

// ---------------- Listados (desplegables y tablas) ----------------
api.MapGet("/clientes", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ClientesAsync())));
api.MapGet("/vehiculos", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.VehiculosAsync())));
api.MapGet("/tickets", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.TicketsAsync())));
api.MapGet("/asesores", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.AsesoresAsync())));
api.MapGet("/servicios", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ServiciosAsync())));

// ---------------- Evidencia de triggers y TRY...CATCH ----------------
api.MapGet("/auditoria", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.AuditoriaAsync())));
api.MapGet("/errores", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ErroresAsync())));

// ---------------- Seguridad ----------------
api.MapPost("/auth/registrar", (LoginDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => { await r.RegistrarUsuarioAsync(dto); return Results.Ok(true); }));
api.MapPost("/auth/login", (LoginDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.LoginAsync(dto))));

// ---------------- Consultas avanzadas (catálogo de Esteban, con lista blanca) ----------------
api.MapGet("/consultas/catalogo", (IConsultasRepository c) => Results.Ok(c.Catalogo()));
api.MapGet("/consultas/{clave}", (string clave, IConsultasRepository c) =>
    Ejecutar(async () =>
    {
        var filas = await c.EjecutarAsync(clave);
        if (filas is null) return Results.NotFound();
        return Results.Ok(filas);
    }));

app.Run();

// Cuerpo que envía la Web al facturar pendientes: { "IdAsesor": 41 }
public record FacturarPendientesRequest(int IdAsesor);
