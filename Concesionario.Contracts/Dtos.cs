
namespace Concesionario.Contracts;

// ==========================================
// FACTURAS
// ==========================================

public class FacturaDto
{
    public int IdFactura { get; set; }
    public DateTime FechaVenta { get; set; }
    public string Cliente { get; set; } = "";
    public string Asesor { get; set; } = "";
    public string NumSerie { get; set; } = "";
    public decimal TotalRepuestos { get; set; }
    public decimal TotalManoObra { get; set; }
    public decimal Total { get; set; }
}
public class TicketCrearDto
{
    public int IdCliente { get; set; }
    public string NumSerie { get; set; } = "";
}

public class VehiculoDto
{
    public string NumSerie { get; set; } = "";
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
}
public class FacturaCrearDto
{
    public int IdTicket { get; set; }
    public int IdAsesor { get; set; }
    public DateTime? FechaVenta { get; set; }
}

// ==========================================
// CLIENTES
// ==========================================

public class ResumenClienteDto
{
    public int IdCliente { get; set; }
    public long CantidadLineas { get; set; }
    public decimal TotalRepuestos { get; set; }
}

public class ClienteDto
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string NombreCompleto => $"{Nombre} {Apellidos}";
}

public class ClienteCrearDto
{
    public string Nombre { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public DateTime? FechaNacimiento { get; set; }
    public string? Telefono { get; set; }
    public string Correo { get; set; } = "";
    public int? IdGenero { get; set; }
    public string? Direccion { get; set; }
    public int? IdCiudad { get; set; }
}

// ==========================================
// DESCUENTOS Y PRECIOS
// ==========================================

public class DescuentoDto
{
    public int IdCliente { get; set; }
    public decimal Monto { get; set; }
    public decimal Descuento { get; set; }
}

public class ActualizarPreciosDto
{
    public string Filtro { get; set; } = "";
    public decimal Porcentaje { get; set; }
}

// ==========================================
// TICKETS
// ==========================================

public class TicketDto
{
    public int IdTicket { get; set; }
    public string Cliente { get; set; } = "";
    public string NumSerie { get; set; } = "";
    public bool Facturado { get; set; }
}

public class TicketPendienteDto
{
    public int IdTicket { get; set; }
    public string Cliente { get; set; } = "";
    public string NumSerie { get; set; } = "";
    public DateTime? FechaSalida { get; set; }
    public decimal TotalEstimado { get; set; }
}

// ==========================================
// ASESORES
// ==========================================

public class AsesorDto
{
    public int IdAsesor { get; set; }
    public string NombreCompleto { get; set; } = "";
}

// ==========================================
// SERVICIOS Y TRABAJOS
// ==========================================

public class ServicioDto
{
    public int IdServicio { get; set; }
    public string NomServicio { get; set; } = "";
}

public class TrabajoCrearDto
{
    public int IdServicio { get; set; }
    public decimal HorasTrabajadas { get; set; }
    public string Descripcion { get; set; } = "";
}

public class AjusteTarifasDto
{
    public int? IdServicio { get; set; }
    public decimal Porcentaje { get; set; }
}

public class TrabajoDetalleDto
{
    public string Servicio { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal HorasTrabajadas { get; set; }
    public decimal TarifaHora { get; set; }
    public decimal Subtotal { get; set; }
}

// ==========================================
// RESULTADO DE PROCEDIMIENTO CON CURSOR
// ==========================================

public class FacturaCursorDto
{
    public int IdFactura { get; set; }
    public int IdTicket { get; set; }
    public string Cliente { get; set; } = "";
    public string NumSerie { get; set; } = "";
    public decimal Total { get; set; }
}

public class ResultadoCursorDto
{
    // Propiedades originales
    public int Marcadas { get; set; }
    public List<TicketPendienteDto> Tickets { get; set; } = new();

    // Propiedades utilizadas en /procedimientos
    public int Facturadas { get; set; }
    public List<FacturaCursorDto> Facturas { get; set; } = new();
}

// ==========================================
// VISTAS SQL SERVER
// ==========================================

public class FacturaResumenDto
{
    public int IdFactura { get; set; }
    public DateTime? FechaVenta { get; set; }
    public string Cliente { get; set; } = "";
    public string Asesor { get; set; } = "";
    public int IdTicket { get; set; }
    public string Marca { get; set; } = "";
    public string Modelo { get; set; } = "";
    public string NumSerie { get; set; } = "";
    public decimal TotalTrabajos { get; set; }
    public decimal TotalRepuestos { get; set; }
    public decimal Total { get; set; }
}

public class IngresoServicioDto
{
    public string NomServicio { get; set; } = "";
    public int CantidadTrabajos { get; set; }
    public decimal HorasTotales { get; set; }
    public decimal IngresosTotales { get; set; }
}

// ==========================================
// FUNCIONES SQL SERVER
// ==========================================

public class TotalFacturaDto
{
    public int IdFactura { get; set; }
    public decimal Total { get; set; }
}

// ==========================================
// AUDITORÍA Y REGISTRO DE ERRORES
// ==========================================

public class AuditoriaDto
{
    public int IdAuditoria { get; set; }
    public string Accion { get; set; } = "";
    public int? IdFactura { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string Usuario { get; set; } = "";
}

public class LogErrorDto
{
    public int IdLog { get; set; }
    public DateTime FechaError { get; set; }
    public int? ErrorNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorProcedure { get; set; }
    public string? Usuario { get; set; }
}

// ==========================================
// SEGURIDAD Y AUTENTICACIÓN
// ==========================================

public class LoginDto
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

// ==========================================
// CATÁLOGOS Y CONSULTAS
// ==========================================

public class CatalogoItemDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

public class ConsultaInfoDto
{
    public string Clave { get; set; } = "";
    public string Titulo { get; set; } = "";
}

// ==========================================
// RESPUESTAS GENERALES DE LA API
// ==========================================

public class IdResultDto
{
    public int Id { get; set; }
}

public class FilasDto
{
    public int Filas { get; set; }
}

public class ApiError
{
    public string Error { get; set; } = "";
    public int Numero { get; set; }
}
