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

static async Task<IResult> Ejecutar(Func<Task<IResult>> accion)
{
    try { return await accion(); }
    catch (SqlException ex) { return Results.BadRequest(new ApiError { Error = ex.Message, Numero = ex.Number }); }
}

var api = app.MapGroup("/api");

// Vistas
api.MapGet("/facturas", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ResumenVentasAsync())));
api.MapGet("/facturas/resumen-por-cliente", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ResumenPorClienteAsync())));

// Funciones
api.MapGet("/facturas/por-fecha", (DateTime desde, DateTime hasta, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.FacturasPorFechaAsync(desde, hasta))));
api.MapGet("/clientes/{id:int}/descuento", (int id, decimal monto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new DescuentoDto { IdCliente = id, Monto = monto, Descuento = await r.DescuentoAsync(id, monto) })));

// Procedimientos almacenados
api.MapPost("/facturas", (FacturaCrearDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new IdResultDto { Id = await r.RegistrarFacturaAsync(dto) })));
api.MapPost("/facturas/procesar-pendientes", (decimal? umbral, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ProcesarPendientesAsync(umbral ?? 500_000m))));
api.MapPost("/repuestos/actualizar-precios", (ActualizarPreciosDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new FilasDto { Filas = await r.ActualizarPreciosAsync(dto) })));

// Clientes (dispara el trigger INSTEAD OF) y evidencia de triggers
api.MapGet("/clientes", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ClientesAsync())));
api.MapPost("/clientes", (ClienteCrearDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(new IdResultDto { Id = await r.RegistrarClienteAsync(dto) })));
api.MapGet("/auditoria", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.AuditoriaAsync())));
api.MapGet("/errores", (IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.ErroresAsync())));

// Catálogos para desplegables (ciudades, generos, asesores, tickets-sin-factura)
api.MapGet("/catalogos/{tipo}", (string tipo, IConcesionarioRepository r) =>
    Ejecutar(async () =>
    {
        var items = await r.CatalogoAsync(tipo);
        return items is null ? Results.NotFound() : Results.Ok(items);
    }));

// Seguridad
api.MapPost("/auth/registro", (LoginDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => { await r.RegistrarUsuarioAsync(dto); return Results.Ok(true); }));
api.MapPost("/auth/login", (LoginDto dto, IConcesionarioRepository r) =>
    Ejecutar(async () => Results.Ok(await r.LoginAsync(dto))));

// Consultas avanzadas (catálogo de Esteban, con lista blanca)
api.MapGet("/consultas", (IConsultasRepository c) => Results.Ok(c.Catalogo()));
api.MapGet("/consultas/{clave}", (string clave, IConsultasRepository c) =>
    Ejecutar(async () =>
    {
        var filas = await c.EjecutarAsync(clave);
        if (filas is null) return Results.NotFound();
        return Results.Ok(filas);
    }));

app.Run();
