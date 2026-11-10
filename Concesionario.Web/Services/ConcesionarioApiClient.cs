using System.Net.Http.Json;
using System.Text.Json;
using Concesionario.Contracts;


namespace Concesionario.WEB.Services;

public class ConcesionarioApiClient
{
    private readonly HttpClient _http;

    public ConcesionarioApiClient(HttpClient http)
    {
        _http = http;
    }

    // Si el API responde con error (400 con {Error, Numero}), se lanza el mensaje real de SQL Server
    // (por ejemplo el THROW del trigger o de un procedimiento) en lugar de un "400" genérico.
    private static async Task Validar(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            if (error is not null && !string.IsNullOrWhiteSpace(error.Error))
                throw new ApiException(error.Error, error.Numero);
        }
        catch (JsonException) { }
        catch (NotSupportedException) { }

        throw new ApiException($"El API respondió {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    private async Task<T> Get<T>(string url)
    {
        var response = await _http.GetAsync(url);
        await Validar(response);

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new Exception("No se recibieron datos del servidor.");
    }

    private async Task<T> Post<T>(string url, object datos)
    {
        var response = await _http.PostAsJsonAsync(url, datos);
        await Validar(response);

        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new Exception("Respuesta vacía del servidor.");
    }

    private async Task Post(string url, object datos)
    {
        var response = await _http.PostAsJsonAsync(url, datos);
        await Validar(response);
    }
    // ERRORES
    public Task<List<LogErrorDto>> Errores()
        => Get<List<LogErrorDto>>("api/errores");

    // CREAR TICKET
    public Task<IdResultDto> CrearTicket(TicketCrearDto datos)
        => Post<IdResultDto>("api/tickets", datos);

    // AUDITORÍA
    public Task<List<AuditoriaDto>> Auditoria()
        => Get<List<AuditoriaDto>>("api/auditoria");
    // CLIENTES
    public Task<List<ClienteDto>> Clientes()
        => Get<List<ClienteDto>>("api/clientes");

    // VEHÍCULOS
    public Task<List<VehiculoDto>> Vehiculos()
        => Get<List<VehiculoDto>>("api/vehiculos");

    // TICKETS
    public Task<List<TicketDto>> Tickets()
        => Get<List<TicketDto>>("api/tickets");

    // ASESORES
    public Task<List<AsesorDto>> Asesores()
        => Get<List<AsesorDto>>("api/asesores");

    // SERVICIOS
    public Task<List<ServicioDto>> Servicios()
        => Get<List<ServicioDto>>("api/servicios");

    // TRABAJOS
    public Task<IdResultDto> AgregarTrabajo(int idTicket, TrabajoCrearDto datos)
        => Post<IdResultDto>($"api/tickets/{idTicket}/trabajos", datos);

    // FACTURAS
    public Task<IdResultDto> FacturarTicket(FacturaCrearDto datos)
        => Post<IdResultDto>("api/facturas", datos);

    // AJUSTAR TARIFAS
    public Task<FilasDto> AjustarTarifas(AjusteTarifasDto datos)
        => Post<FilasDto>("api/servicios/ajustar-tarifas", datos);

    // FACTURAR TICKETS PENDIENTES
    public Task<ResultadoCursorDto> FacturarPendientes(int idAsesor)
        => Post<ResultadoCursorDto>(
            "api/facturas/facturar-pendientes",
            new { IdAsesor = idAsesor });

    // AUTENTICACIÓN
    public Task Registrar(LoginDto datos)
        => Post("api/auth/registrar", datos);

    public async Task<bool> Ingresar(LoginDto datos)
    {
        var resultado = await Post<bool>("api/auth/login", datos);
        return resultado;
    }

    // VISTAS
    public Task<List<FacturaResumenDto>> Facturas()
        => Get<List<FacturaResumenDto>>("api/vistas/facturas");

    public Task<List<IngresoServicioDto>> IngresosPorServicio()
        => Get<List<IngresoServicioDto>>("api/vistas/ingresos-servicio");

    // FUNCIONES
    public Task<TotalFacturaDto> TotalFactura(int idFactura)
        => Get<TotalFacturaDto>($"api/funciones/total-factura/{idFactura}");

    public Task<List<TrabajoDetalleDto>> TrabajosDeTicket(int idTicket)
        => Get<List<TrabajoDetalleDto>>($"api/funciones/trabajos-ticket/{idTicket}");

    // CONSULTAS AVANZADAS
    public Task<List<ConsultaInfoDto>> CatalogoConsultas()
        => Get<List<ConsultaInfoDto>>("api/consultas/catalogo");

    public Task<List<Dictionary<string, JsonElement>>> Consulta(string clave)
        => Get<List<Dictionary<string, JsonElement>>>(
            $"api/consultas/{Uri.EscapeDataString(clave)}");
}
