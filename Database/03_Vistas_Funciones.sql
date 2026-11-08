USE Concesionario;
GO

--VISTAS Y FUNCIONES(03_VISTAS_FUNCIONES)

-- 1. VISTA ESTÁNDAR: Resumen de facturas y totales calculados
CREATE OR ALTER VIEW dbo.vw_ResumenFacturas AS
SELECT 
    F.idFactura, 
    F.FechaVenta,
    PC.Nombre + ' ' + PC.Apellidos AS Cliente,
    PA.Nombre + ' ' + PA.Apellidos AS Asesor,
    T.idTicket, 
    V.NumSerie, 
    Ma.NomMarca AS Marca, 
    Mo.NomModelo AS Modelo,
    ISNULL(TR.TotalTrabajos, 0) AS TotalTrabajos,
    ISNULL(RP.TotalRepuestos, 0) AS TotalRepuestos,
    ISNULL(TR.TotalTrabajos, 0) + ISNULL(RP.TotalRepuestos, 0) AS Total
FROM dbo.Facturas F
JOIN dbo.Tickets T ON T.idTicket = F.idTicket
JOIN dbo.Personas PC ON PC.idPersona = T.idCliente
JOIN dbo.Personas PA ON PA.idPersona = F.idAsesor
JOIN dbo.Vehiculos V ON V.NumSerie = T.NumSerie
JOIN dbo.Marcas Ma ON Ma.idMarca = V.idMarca
JOIN dbo.Modelos Mo ON Mo.idModelo = V.idModelo
OUTER APPLY (
    SELECT SUM(W.HorasTrabajadas * S.TarifaHora) AS TotalTrabajos
    FROM dbo.Trabajos W 
    JOIN dbo.Servicios S ON S.idServicio = W.idServicio
    WHERE W.idTicket = T.idTicket
) TR
OUTER APPLY (
    SELECT SUM(R.Cantidad * R.PrecioUnitario) AS TotalRepuestos
    FROM dbo.TicketRepuesto R 
    WHERE R.idTicket = T.idTicket
) RP;
GO


-- 2. VISTA INDEXADA: Ingresos por servicio (Requiere SCHEMABINDING y COUNT_BIG)
CREATE OR ALTER VIEW dbo.vw_IngresosPorServicio 
WITH SCHEMABINDING 
AS
SELECT 
    S.idServicio, 
    S.NomServicio,
    COUNT_BIG(*) AS CantidadTrabajos,
    SUM(W.HorasTrabajadas) AS HorasTotales,
    SUM(W.HorasTrabajadas * S.TarifaHora) AS IngresosTotales
FROM dbo.Trabajos W
JOIN dbo.Servicios S ON S.idServicio = W.idServicio
GROUP BY S.idServicio, S.NomServicio;
GO


-- Creación del Índice Clustered para materializar la vista indexada
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_vw_IngresosPorServicio' AND object_id = OBJECT_ID('dbo.vw_IngresosPorServicio'))
CREATE UNIQUE CLUSTERED INDEX IX_vw_IngresosPorServicio 
ON dbo.vw_IngresosPorServicio (idServicio);
GO


-- 3. FUNCIÓN ESCALAR: Calcula el total de una factura específica por su ID
CREATE OR ALTER FUNCTION dbo.fn_TotalFactura (@idFactura INT)
RETURNS DECIMAL(18,2) AS
BEGIN
    DECLARE @total DECIMAL(18,2) = 0;
    
    SELECT @total = ISNULL((
        SELECT SUM(W.HorasTrabajadas * S.TarifaHora)
        FROM dbo.Trabajos W 
        JOIN dbo.Servicios S ON S.idServicio = W.idServicio
        WHERE W.idTicket = F.idTicket
    ), 0) + ISNULL((
        SELECT SUM(R.Cantidad * R.PrecioUnitario)
        FROM dbo.TicketRepuesto R 
        WHERE R.idTicket = F.idTicket
    ), 0)
    FROM dbo.Facturas F
    WHERE F.idFactura = @idFactura;

    RETURN @total;
END;
GO


-- 4. FUNCIÓN DE TABLA EN LÍNEA: Detalla los trabajos realizados en un ticket
CREATE OR ALTER FUNCTION dbo.fn_TrabajosPorTicket (@idTicket INT)
RETURNS TABLE AS
RETURN (
    SELECT 
        W.idTrabajo, 
        S.NomServicio AS Servicio, 
        W.Descripcion,
        W.HorasTrabajadas, 
        S.TarifaHora,
        (W.HorasTrabajadas * S.TarifaHora) AS Subtotal
    FROM dbo.Trabajos W
    JOIN dbo.Servicios S ON S.idServicio = W.idServicio
    WHERE W.idTicket = @idTicket
);
GO