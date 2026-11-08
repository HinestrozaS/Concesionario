using Concesionario.Contracts;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Concesionario.Api.Repositories;

public interface IConsultasRepository
{
    IEnumerable<ConsultaInfoDto> Catalogo();
    Task<IEnumerable<IDictionary<string, object>>?> EjecutarAsync(string clave);
}

public class ConsultasRepository : IConsultasRepository
{
    private readonly string _cs;
    public ConsultasRepository(IConfiguration config) =>
        _cs = config.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Falta ConnectionStrings:DefaultConnection");

    private static readonly Dictionary<string, (string Titulo, string Sql)> Consultas = new()
    {
        // ---- JOINS ----
        ["join-natural"] = ("JOIN simple (equivale a INNER JOIN): clientes con su persona y ciudad",
            "SELECT P.Nombre, P.Apellidos, C.Direccion, Ci.NomCiudad FROM dbo.Clientes AS C JOIN dbo.Personas AS P ON P.idPersona = C.idPersona JOIN dbo.Ciudades AS Ci ON Ci.idCiudad = C.idCiudad"),

        ["inner-join"] = ("INNER JOIN: tickets con su cliente y su vehículo",
            "SELECT T.idTicket, P.Nombre, P.Apellidos, V.NumSerie, T.FechaEntrada FROM dbo.Tickets T INNER JOIN dbo.Clientes C ON C.idPersona = T.idCliente INNER JOIN dbo.Personas P ON P.idPersona = C.idPersona INNER JOIN dbo.Vehiculos V ON V.NumSerie = T.NumSerie"),

        ["left-join"] = ("LEFT JOIN: todos los vehículos y sus tickets (si tienen)",
            "SELECT V.NumSerie, Mo.NomModelo, T.idTicket, T.FechaEntrada FROM dbo.Vehiculos V JOIN dbo.Modelos Mo ON Mo.idModelo = V.idModelo LEFT JOIN dbo.Tickets T ON T.NumSerie = V.NumSerie"),

        ["right-join"] = ("RIGHT JOIN: todos los tickets, con o sin factura",
            "SELECT T.idTicket, T.NumSerie, F.idFactura FROM dbo.Facturas F RIGHT JOIN dbo.Tickets T ON T.idTicket = F.idTicket"),

        ["full-join"] = ("FULL OUTER JOIN: especialidades y técnicos",
            "SELECT E.NomEspecialidad, TE.idTecnico FROM dbo.Especialidades E FULL OUTER JOIN dbo.TecnicoEspecialidad TE ON TE.idEspecialidad = E.idEspecialidad"),

        ["cross-join"] = ("CROSS JOIN: marcas x tipos de vehículo",
            "SELECT Ma.NomMarca, Ti.NomTipo FROM dbo.Marcas Ma CROSS JOIN dbo.Tipos Ti"),

        ["anti-join"] = ("Anti-JOIN: clientes sin ningún ticket",
            "SELECT P.Nombre, P.Apellidos FROM dbo.Clientes C JOIN dbo.Personas P ON P.idPersona = C.idPersona LEFT JOIN dbo.Tickets T ON T.idCliente = C.idPersona WHERE T.idTicket IS NULL"),

        // ---- Teoría de conjuntos ----
        ["union"] = ("UNION: nombres de clientes y técnicos (sin duplicados)",
            "SELECT P.Nombre, P.Apellidos FROM dbo.Personas P WHERE P.idPersona IN (SELECT idPersona FROM dbo.Clientes) UNION SELECT P.Nombre, P.Apellidos FROM dbo.Personas P WHERE P.idPersona IN (SELECT idPersona FROM dbo.Tecnicos)"),

        ["union-all"] = ("UNION ALL: conserva duplicados",
            "SELECT idPersona FROM dbo.Clientes UNION ALL SELECT idPersona FROM dbo.Tecnicos"),

        ["except"] = ("EXCEPT: personas que no son clientes",
            "SELECT idPersona FROM dbo.Personas EXCEPT SELECT idPersona FROM dbo.Clientes"),

        ["intersect"] = ("INTERSECT: personas que son clientes y técnicos a la vez",
            "SELECT idPersona FROM dbo.Clientes INTERSECT SELECT idPersona FROM dbo.Tecnicos"),

        // ---- Operadores especiales ----
        ["any"] = ("ANY: servicios más caros que alguno de los 3 primeros",
            "SELECT idServicio, NomServicio, TarifaHora FROM dbo.Servicios WHERE TarifaHora > ANY (SELECT TarifaHora FROM dbo.Servicios WHERE idServicio <= 3)"),

        ["all"] = ("ALL: servicios más caros que todos los 3 primeros",
            "SELECT idServicio, NomServicio, TarifaHora FROM dbo.Servicios WHERE TarifaHora > ALL (SELECT TarifaHora FROM dbo.Servicios WHERE idServicio <= 3)"),

        ["like-porcentaje"] = ("LIKE con %: nombres que empiezan por M",
            "SELECT idPersona, Nombre, Apellidos FROM dbo.Personas WHERE Nombre LIKE 'M%'"),

        ["like-guion"] = ("LIKE con _: segunda letra 'a'",
            "SELECT idPersona, Nombre, Apellidos FROM dbo.Personas WHERE Nombre LIKE '_a%'"),

        ["like-corchetes"] = ("LIKE con [ ]: empiezan por C, D o M",
            "SELECT idPersona, Nombre, Apellidos FROM dbo.Personas WHERE Nombre LIKE '[CDM]%'"),

        ["like-negado"] = ("LIKE con [^]: NO empiezan entre A y M",
            "SELECT idPersona, Nombre, Apellidos FROM dbo.Personas WHERE Nombre LIKE '[^A-M]%'"),

        // ---- Funciones de fecha, cadena y numéricas ----
        ["fechas"] = ("Funciones de fecha sobre los tickets",
            "SELECT idTicket, FechaEntrada, FechaSalida, YEAR(FechaEntrada) AS Anio, DATENAME(MONTH, FechaEntrada) AS Mes, DATEDIFF(DAY, FechaEntrada, FechaSalida) AS DiasEnTaller, EOMONTH(FechaEntrada) AS FinDeMes FROM dbo.Tickets"),

        ["cadenas"] = ("Funciones de cadena sobre las personas",
            "SELECT UPPER(Nombre) AS Mayusculas, LEN(Nombre) AS Largo, LEFT(Apellidos, 3) AS Inicial3, CONCAT(Nombre, ' ', Apellidos) AS NombreCompleto, REPLACE(Telefono, '300', '***') AS TelefonoOculto FROM dbo.Personas"),

        ["numericas"] = ("Funciones numéricas sobre los repuestos",
            "SELECT idPieza, PrecioCompra, PrecioVenta, ROUND((PrecioVenta - PrecioCompra) * 100.0 / NULLIF(PrecioCompra, 0), 1) AS MargenPct, CEILING(PrecioVenta * 1.19) AS ConIva, FLOOR(PrecioVenta / 1000) AS Miles, ABS(PrecioVenta - PrecioCompra) AS Diferencia FROM dbo.Repuestos"),

        // ---- Atributo derivado y agrupación ----
        ["derivado"] = ("Atributo derivado: subtotal = cantidad x precio unitario",
            "SELECT R.idTicket, R.idPieza, R.Cantidad, R.PrecioUnitario, R.Cantidad * R.PrecioUnitario AS Subtotal FROM dbo.TicketRepuesto R"),

        ["group-having"] = ("GROUP BY y HAVING: vehículos y valor promedio por marca",
            "SELECT Ma.NomMarca, COUNT(*) AS Vehiculos, AVG(V.Valor) AS ValorPromedio FROM dbo.Vehiculos V JOIN dbo.Marcas Ma ON Ma.idMarca = V.idMarca GROUP BY Ma.NomMarca HAVING COUNT(*) >= 1")
    };

    public IEnumerable<ConsultaInfoDto> Catalogo() =>
        Consultas.Select(c => new ConsultaInfoDto { Clave = c.Key, Titulo = c.Value.Titulo });

    public async Task<IEnumerable<IDictionary<string, object>>?> EjecutarAsync(string clave)
    {
        if (!Consultas.TryGetValue(clave, out var q)) return null;
        using var db = new SqlConnection(_cs);
        var filas = await db.QueryAsync(q.Sql);
        return filas.Cast<IDictionary<string, object>>().ToList();
    }
}
