USE Concesionario;
GO

SELECT * FROM Especialidades;
SELECT * FROM Facturas;
SELECT * FROM Asesores;
SELECT * FROM Generos;
SELECT * FROM Ciudades;
SELECT * FROM Clientes;
SELECT * FROM Colores;
SELECT * FROM Marcas;
SELECT * FROM Modelos;
SELECT * FROM Personas;
SELECT * FROM Repuestos;
SELECT * FROM Servicios;
SELECT * FROM TecnicoEspecialidad;
SELECT * FROM Tecnicos;
SELECT * FROM TicketRepuesto;
SELECT * FROM Tickets;
SELECT * FROM Tipos;
SELECT * FROM Trabajos;
SELECT * FROM Vehiculos;

INSERT INTO Generos (NomGenero) VALUES
('Agénero'),
('Bigénero'),
('Pangénero'),
('Género fluido'),
('Andrógino'),
('Neutrois'),
('Transgénero'),
('Hombre trans'),
('Mujer trans'),
('Intersexual'),
('Dos espíritus'),
('Demigénero'),
('Trigénero'),
('Aporagénero'),
('Tercer sexo'),
('Otro / No especificado');

SELECT * FROM Generos;


---VERIFICACION DE ESQUEMA E INSERCION DE DATOS


-- 1. Completar Ciudades (mínimo 20 filas)
DECLARE @n INT;
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Ciudades;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Ciudades (NomCiudad) SELECT CONCAT('Ciudad', k) FROM N;

-- 2. Completar Generos
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Generos;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Generos (NomGenero) SELECT CONCAT('Género', k) FROM N;

-- 3. Completar Especialidades
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Especialidades;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Especialidades (NomEspecialidad) SELECT CONCAT('Especialidad ', k) FROM N;

-- 4. Completar Marcas
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Marcas;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Marcas (NomMarca) SELECT CONCAT('Marca', k) FROM N;

-- 5. Completar Modelos
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Modelos;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Modelos (NomModelo) SELECT CONCAT('Modelo', k) FROM N;

-- 6. Completar Tipos
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Tipos;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Tipos (NomTipo) SELECT CONCAT('Tipo', k) FROM N;

-- 7. Completar Colores
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Colores;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Colores (NomColor) SELECT CONCAT('Color', k) FROM N;

-- 8. Completar Servicios
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Servicios;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Servicios (NomServicio, TarifaHora) SELECT CONCAT('Servicio ', k), 40000 + k * 500 FROM N;

-- 9. Completar Repuestos
SELECT @n = CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Repuestos;
WITH N AS (SELECT TOP (@n) 100 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS k FROM sys.all_objects)
INSERT INTO dbo.Repuestos (DescripcionRep, PrecioCompra, PrecioVenta) SELECT CONCAT('Repuesto ', k), 20000 + k * 100, 30000 + k * 150 FROM N;

-- 10. Facturas (una por cada ticket que aún no tenga, respetando el UNIQUE)
INSERT INTO dbo.Facturas (FechaVenta, idAsesor, idTicket)
SELECT TOP ((SELECT CASE WHEN COUNT(*) >= 20 THEN 0 ELSE 20 - COUNT(*) END FROM dbo.Facturas))
CAST(GETDATE() AS DATE), (SELECT MIN(idPersona) FROM dbo.Asesores), T.idTicket
FROM dbo.Tickets T
WHERE NOT EXISTS (SELECT 1 FROM dbo.Facturas F WHERE F.idTicket = T.idTicket);
GO