USE Concesionario;
GO


--LOS 25 EJERCICIOS DDL/DML (02_EJERCICIOS_DDL_DML)


-- CATEGORÍA 1: 5 sentencias DELETE (con Transacción y Rollback)
 
BEGIN TRANSACTION;

-- Ejercicio 1: Eliminar repuestos en tickets con cantidad 0
DELETE FROM dbo.TicketRepuesto WHERE Cantidad = 0;
SELECT @@ROWCOUNT AS Eliminados_TicketRepuesto;

-- Ejercicio 2: Eliminar trabajos sin horas registradas
DELETE FROM dbo.Trabajos WHERE HorasTrabajadas = 0;
SELECT @@ROWCOUNT AS Eliminados_Trabajos;

-- Ejercicio 3: Eliminar tickets antiguos sin salida y sin detalles
DELETE T FROM dbo.Tickets T
WHERE T.FechaSalida IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Trabajos W WHERE W.idTicket = T.idTicket)
  AND NOT EXISTS (SELECT 1 FROM dbo.TicketRepuesto R WHERE R.idTicket = T.idTicket)
  AND NOT EXISTS (SELECT 1 FROM dbo.Facturas F WHERE F.idTicket = T.idTicket);
SELECT @@ROWCOUNT AS Eliminados_Tickets;

-- Ejercicio 4: Eliminar clientes que no tienen ningún ticket asociado
DELETE C FROM dbo.Clientes C 
WHERE NOT EXISTS (SELECT 1 FROM dbo.Tickets T WHERE T.idCliente = C.idPersona);
SELECT @@ROWCOUNT AS Eliminados_Clientes;

-- Ejercicio 5: Eliminar repuestos baratos no utilizados
DELETE P FROM dbo.Repuestos P
WHERE P.PrecioVenta < 1000 
  AND NOT EXISTS (SELECT 1 FROM dbo.TicketRepuesto R WHERE R.idPieza = P.idPieza);
SELECT @@ROWCOUNT AS Eliminados_Repuestos;

ROLLBACK TRANSACTION;
GO


-- CATEGORÍA 2: 5 sentencias UPDATE



-- Ejercicio 6: Actualizar precio de venta de repuestos con margen bajo
UPDATE dbo.Repuestos SET PrecioVenta = ROUND(PrecioVenta * 1.10, 2) WHERE PrecioVenta < PrecioCompra * 1.20;

-- Ejercicio 7: Incrementar tarifa de servicios principales
UPDATE dbo.Servicios SET TarifaHora = ROUND(TarifaHora * 1.05, 2) WHERE idServicio <= 3;

-- Ejercicio 8: Aplicar depreciación a vehículos de tipo 1
UPDATE dbo.Vehiculos SET Valor = ROUND(Valor * 0.95, 2) WHERE Valor IS NOT NULL AND idTipo = 1;

-- Ejercicio 9: Actualizar teléfono de persona con ID 1
UPDATE dbo.Personas SET Telefono = '3001234567' WHERE idPersona = 1;

-- Ejercicio 10: Asignar fecha de salida a tickets antiguos pendientes
UPDATE dbo.Tickets SET FechaSalida = DATEADD(DAY, 3, FechaEntrada)
WHERE FechaSalida IS NULL AND FechaEntrada < DATEADD(DAY, -30, CAST(GETDATE() AS DATE));
GO




-- CATEGORÍA 3: 5 sentencias ALTER TABLE... DROP COLUMN 
-- (Se usa verificación con COL_LENGTH para evitar errores si se reejecuta)

IF COL_LENGTH('dbo.Servicios', 'CodigoInterno') IS NULL ALTER TABLE dbo.Servicios ADD CodigoInterno VARCHAR(20) NULL;
IF COL_LENGTH('dbo.Vehiculos', 'ColorSecundario') IS NULL ALTER TABLE dbo.Vehiculos ADD ColorSecundario VARCHAR(30) NULL;
IF COL_LENGTH('dbo.Clientes', 'CodigoPostalAntiguo') IS NULL ALTER TABLE dbo.Clientes ADD CodigoPostalAntiguo VARCHAR(10) NULL;
IF COL_LENGTH('dbo.Repuestos', 'Ubicacion') IS NULL ALTER TABLE dbo.Repuestos ADD Ubicacion VARCHAR(30) NULL;
IF COL_LENGTH('dbo.Tickets', 'ObservacionesInternas') IS NULL ALTER TABLE dbo.Tickets ADD ObservacionesInternas VARCHAR(200) NULL;
GO


-- Ejercicios 11 a 15: Eliminación de columnas temporales

ALTER TABLE dbo.Servicios DROP COLUMN CodigoInterno;
ALTER TABLE dbo.Vehiculos DROP COLUMN ColorSecundario;
ALTER TABLE dbo.Clientes DROP COLUMN CodigoPostalAntiguo;
ALTER TABLE dbo.Repuestos DROP COLUMN Ubicacion;
ALTER TABLE dbo.Tickets DROP COLUMN ObservacionesInternas;
GO



-- CATEGORÍA 4: 5 sentencias ALTER TABLE... ALTER COLUMN (Tipos de datos)

-- Ejercicios 16 a 20: Modificar restricciones de nulabilidad y tamaños
ALTER TABLE dbo.Personas ALTER COLUMN Correo VARCHAR(200) NULL;
ALTER TABLE dbo.Servicios ALTER COLUMN TarifaHora DECIMAL(12,2) NOT NULL;
ALTER TABLE dbo.Repuestos ALTER COLUMN PrecioVenta DECIMAL(12,2) NOT NULL;
ALTER TABLE dbo.Clientes ALTER COLUMN Direccion VARCHAR(150) NULL;
ALTER TABLE dbo.Vehiculos ALTER COLUMN Motor VARCHAR(80) NOT NULL;
GO



-- CATEGORÍA 5: 5 sentencias ALTER TABLE... ADD COLUMN

-- Ejercicios 21 a 25: Agregar nuevas columnas con valores por defecto
IF COL_LENGTH('dbo.Personas', 'LugarNacimiento') IS NULL ALTER TABLE dbo.Personas ADD LugarNacimiento VARCHAR(60) NULL;
IF COL_LENGTH('dbo.Facturas', 'MetodoPago') IS NULL ALTER TABLE dbo.Facturas ADD MetodoPago VARCHAR(30) NULL DEFAULT 'Efectivo';
IF COL_LENGTH('dbo.Vehiculos', 'GarantiaMeses') IS NULL ALTER TABLE dbo.Vehiculos ADD GarantiaMeses INT NULL DEFAULT 12;
IF COL_LENGTH('dbo.Repuestos', 'CodigoUbicacion') IS NULL ALTER TABLE dbo.Repuestos ADD CodigoUbicacion VARCHAR(20) NULL;
IF COL_LENGTH('dbo.Tickets', 'Prioridad') IS NULL ALTER TABLE dbo.Tickets ADD Prioridad VARCHAR(10) NULL DEFAULT 'Normal';
GO
