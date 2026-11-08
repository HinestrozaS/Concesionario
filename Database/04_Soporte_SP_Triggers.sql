USE Concesionario;
GO


CREATE TABLE dbo.LogErrores ( 
	IdLog INT IDENTITY(1,1) PRIMARY KEY, 
	FechaError DATETIME NOT NULL DEFAULT GETDATE(), 
	ErrorNumber INT NULL, ErrorMessage NVARCHAR(4000) NULL, 
	ErrorProcedure NVARCHAR(126) NULL, 
	Usuario VARCHAR(128) NULL DEFAULT SUSER_SNAME()
);
GO

CREATE TABLE dbo.AuditoriaFacturas ( 
	IdAuditoria INT IDENTITY(1,1) PRIMARY KEY, 
	Accion VARCHAR(30) NOT NULL, 
	idFactura INT NULL, 
	FechaRegistro DATETIME NOT NULL DEFAULT GETDATE(), 
	Usuario VARCHAR(128) NULL DEFAULT SUSER_SNAME()
);
GO


--1. Crea un ticket. Dispara trg_Tickets_UnicoAbierto (INSTEAD OF) y devuelve el id
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarTicket @idCliente INT, @NumSerie VARCHAR(100), @idTicketGenerado INT OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	--Aborta y deshace la transacción automáticamente cuándo ocurre un error grave
	SET XACT_ABORT ON; 


	BEGIN TRY 
		BEGIN TRANSACTION;

			IF NOT EXISTS (
				SELECT 1 
				FROM dbo.Clientes 
				WHERE idPersona = @idCliente) 
			THROW 50001, 'El cliente no existe.', 1; 

			IF NOT EXISTS (
				SELECT 1 
				FROM dbo.Vehiculos 
				WHERE NumSerie = @NumSerie) 
			THROW 50002, 'El vehículo no existe.', 1;

		INSERT INTO dbo.Tickets (FechaEntrada, idCliente, NumSerie)
		VALUES (CAST(GETDATE() AS DATE), @idCliente, @NumSerie);

		--Se selecciona el máximo ticket de un Cliente (un cliente 1:N tickets)
		SELECT @idTicketGenerado = MAX(idTicket) 
		FROM dbo.Tickets 
		WHERE idCliente = @idCliente 
			AND NumSerie = @NumSerie; 
		
		COMMIT TRANSACTION; 
	END TRY
	BEGIN CATCH 
		IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

		INSERT INTO dbo.LogErrores (ErrorNumber, ErrorMessage, ErrorProcedure)
		VALUES (ERROR_NUMBER(), ERROR_MESSAGE(), ERROR_PROCEDURE()); 
		THROW;

	END CATCH
END;
GO


--2.Agrega un trabajo a un ticket
CREATE OR ALTER PROCEDURE dbo.sp_AgregarTrabajo @idTicket INT, @idServicio INT, @HorasTrabajadas DECIMAL(4,2), 
						@Descripcion VARCHAR(MAX), @idTrabajoGenerado INT OUTPUT

AS
BEGIN
	
	SET NOCOUNT ON;
	--Aborta y deshace la transacción automáticamente cuándo ocurre un error grave
	SET XACT_ABORT ON;

	BEGIN TRY 
		IF NOT EXISTS (
			SELECT 1 
			FROM dbo.Tickets 
			WHERE idTicket = @idTicket) 
		--Sirve para arrojar un error en dado caso aparezca
		THROW 50011, 'El ticket no existe.', 1; 
		
		IF EXISTS (
			SELECT 1 
			FROM dbo.Facturas 
			WHERE idTicket = @idTicket) 
		THROW 50012, 'El ticket ya fue facturado: no admite más trabajos.', 1; 
		
		IF NOT EXISTS (
			SELECT 1 
			FROM dbo.Servicios 
			WHERE idServicio = @idServicio) 
		THROW 50013, 'El servicio no existe.', 1; 
		
		
		IF @HorasTrabajadas <= 0 
			THROW 50014, 'Las horas trabajadas deben ser mayores a cero.', 1;

		INSERT INTO dbo.Trabajos (HorasTrabajadas, Descripcion, idTicket, idServicio) 
		VALUES (@HorasTrabajadas, @Descripcion, @idTicket, @idServicio); 
		
		--SCOPE_IDENTITY() --> Se utiliza para obtener el último ID registrado, sólo funciona con IDENTITY
		SET @idTrabajoGenerado = SCOPE_IDENTITY();
		
	END TRY
	BEGIN CATCH

		INSERT INTO dbo.LogErrores (ErrorNumber, ErrorMessage, ErrorProcedure) 
		VALUES (ERROR_NUMBER(), ERROR_MESSAGE(), ERROR_PROCEDURE()); 
		THROW;

	END CATCH


END;
GO



--3. Factura un ticket
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarFacturaTicket @idTicket INT, @idAsesor INT, @idFacturaGenerada INT OUTPUT
AS
BEGIN
	
	SET NOCOUNT ON; 
	SET XACT_ABORT ON; 


	BEGIN TRY 
		BEGIN TRANSACTION;

		IF NOT EXISTS(
			SELECT 1
			FROM dbo.Tickets 
			WHERE idTicket = @idTicket)
		THROW 50021, 'El ticket no existe.', 1;


		IF NOT EXISTS (
			SELECT 1 
			FROM dbo.Asesores 
			WHERE idPersona = @idAsesor) 
		THROW 50022, 'El asesor no existe.', 1;

		IF EXISTS (
			SELECT 1 
			FROM dbo.Facturas 
			WHERE idTicket = @idTicket) 
		THROW 50023, 'El ticket ya tiene factura.', 1; 
		
		IF NOT EXISTS (
			SELECT 1 
			FROM dbo.Trabajos 
			WHERE idTicket = @idTicket) 
		THROW 50024, 'El ticket no tiene trabajos registrados.', 1;

		--CAST() --> Pasa de un tipo de variable a otro 
		INSERT INTO dbo.Facturas (FechaVenta, idAsesor, idTicket) 
		VALUES (CAST(GETDATE() AS DATE), @idAsesor, @idTicket); 
		

		--SCOPE_IDENTITY() --> Se utiliza para obtener el último ID registrado, sólo funciona con IDENTITY
		SET @idFacturaGenerada = SCOPE_IDENTITY(); 
		
		COMMIT TRANSACTION; 
	END TRY
	BEGIN CATCH 
		IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; 
		
		INSERT INTO dbo.LogErrores (ErrorNumber, ErrorMessage, ErrorProcedure) 
		VALUES (ERROR_NUMBER(), ERROR_MESSAGE(), ERROR_PROCEDURE()); 
		THROW;

	END CATCH
END;
GO


/* 4. Ajustar Tarifas de Servicio
Realiza una actualización masiva de los precios por hora de los servicios
del taller aplicando un porcentaje de incremento. */

CREATE OR ALTER PROCEDURE dbo.sp_AjustarTarifasServicios @Porcentaje DECIMAL(5,2), @idServicio INT = NULL, 
		@FilasImpactadas INT OUTPUT

AS 
BEGIN
	
	SET NOCOUNT ON; 
	
	BEGIN TRY 
		IF @Porcentaje < -50 OR @Porcentaje > 100 
			THROW 50030, 'El porcentaje debe estar entre -50 y 100.', 1; 
		
		UPDATE dbo.Servicios 
		/*Recalcula la tarifa hora: toma el valor actual, le suma el porcentaje (positivo o negativo) 
		y redondea el resultado a 2 decimales*/
		SET TarifaHora = ROUND(TarifaHora * (1 + @Porcentaje / 100), 2)
		WHERE @idServicio IS NULL OR idServicio = @idServicio; 
		
		-- @@ROWCOUNT --> Cuenta el número de filas afectadas
		SET @FilasImpactadas = @@ROWCOUNT; 
	END TRY
	BEGIN CATCH

		INSERT INTO dbo.LogErrores (ErrorNumber, ErrorMessage, ErrorProcedure)
		VALUES (ERROR_NUMBER(), ERROR_MESSAGE(), ERROR_PROCEDURE()); 
		THROW;
	END CATCH

END;
GO

-- 5. Factura todos los tickets con trabajos y sin factura

CREATE OR ALTER PROCEDURE dbo.sp_FacturarTicketsPendientesCursor
		@idAsesor INT, @Facturadas INT OUTPUT
		
AS
BEGIN
		
		SET NOCOUNT ON; 
		--Sirve para abortar y deshacer una trasacción automáticamente cuándo aparece un error grave
		SET XACT_ABORT ON;

		DECLARE @idTicket INT; 
		--Tabla temporal en memoria para guardar los IDs de las facturas que se vayan creando
		DECLARE @creadas TABLE (idFactura INT); 
		SET @Facturadas = 0;

	BEGIN TRY 
			IF NOT EXISTS (
				SELECT 1 
				FROM dbo.Asesores 
				WHERE idPersona = @idAsesor) 
			THROW 50040, 'El asesor no existe.', 1;

			BEGIN TRANSACTION;

			-- Declara un cursor local de avance rápido (FAST_FORWARD) optimizado para recorrer consultas eficientemente
			DECLARE cur_Tickets CURSOR LOCAL FAST_FORWARD FOR
			
				SELECT T.idTicket 
				FROM dbo.Tickets T 
				WHERE 
				-- Condición 1: El ticket debe tener obligatoriamente trabajos asociados
				EXISTS (
						SELECT 1 
						FROM dbo.Trabajos W 
						WHERE W.idTicket = T.idTicket)
				-- Condición 2: El ticket NO debe tener una factura previa registrada
					AND NOT EXISTS (
						SELECT 1 
						FROM dbo.Facturas F 
						WHERE F.idTicket = T.idTicket)

			ORDER BY T.idTicket;

			--Abre el cursos
			OPEN cur_Tickets; 
			--Sirve para leer la siguiente fila
			FETCH NEXT FROM cur_Tickets INTO @idTicket; 

			/*Se repite mientras el cursor encuentre registros por procesar 
			(@@FETCH_STATUS = 0 indica éxito en la lectura, sino encuentra más filas arrojaría -1 y para el bucle)*/
			WHILE @@FETCH_STATUS = 0 
			BEGIN

				INSERT INTO dbo.Facturas (FechaVenta, idAsesor, idTicket) 
				VALUES (CAST(GETDATE() AS DATE), @idAsesor, @idTicket); 
				
				-- Captura el ID de la factura recién creada y lo guarda en la tabla temporal `@creadas`
				INSERT INTO @creadas (idFactura) 
				VALUES (SCOPE_IDENTITY()); 

				-- Aumenta en 1 el contador de facturas generadas
				SET @Facturadas += 1; 
				
				FETCH NEXT FROM cur_Tickets INTO @idTicket; 
			END

			CLOSE cur_Tickets; 
			--libera los recursos del cursor de la memoria
			DEALLOCATE cur_Tickets;
			COMMIT TRANSACTION;

			SELECT V.* 
			FROM dbo.vw_ResumenFacturas V 
			JOIN @creadas C ON C.idFactura = V.idFactura 
			ORDER BY V.idFactura;

	END TRY
	BEGIN CATCH
		
		/*Manejo de errores del cursor: Si ocurre un error y el cursor sigue abierto, se asegura de cerrarlo y 
		liberarlo para evitar fugas de memoria*/
		IF CURSOR_STATUS('local', 'cur_Tickets') >= 0 
		
		BEGIN 
			CLOSE cur_Tickets; 
			DEALLOCATE cur_Tickets; 
		END 
		
		IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION; 
		INSERT INTO dbo.LogErrores (ErrorNumber, ErrorMessage, ErrorProcedure)
		VALUES (ERROR_NUMBER(), ERROR_MESSAGE(), ERROR_PROCEDURE());
		THROW;
	END CATCH

END;
GO


---------------------------------------- TRIGGERS ----------------------------------------------------------
--Un vehículo no puede tener dos tickets abiertos (sin FechaSalida)

CREATE OR ALTER TRIGGER dbo.trg_Tickets_UnicoAbierto ON dbo.Tickets
INSTEAD OF INSERT 
AS
BEGIN

	SET NOCOUNT ON; 
	
	/*Comprueba si el vehículo que intentan registrar ya posee un ticket 
       que sigue abierto (es decir, que su 'FechaSalida' es NULL).*/
	IF EXISTS (
		SELECT 1 
		FROM inserted I 
		JOIN dbo.Tickets T ON T.NumSerie = I.NumSerie 
			AND T.FechaSalida IS NULL)
			
	THROW 50010, 'ERROR: El vehículo ya tiene un ticket abierto (sin fecha de salida).', 1; 
	
	
	INSERT INTO dbo.Tickets (FechaEntrada, FechaSalida, idCliente, NumSerie)
	SELECT FechaEntrada, FechaSalida, idCliente, NumSerie 
	FROM inserted; -- Toma los datos directamente desde la tabla virtual 'inserted'
END;
GO


--Auditoría automática de Facturas

CREATE OR ALTER TRIGGER dbo.trg_AuditoriaFacturas ON dbo.Facturas
AFTER INSERT, UPDATE, DELETE 

AS
BEGIN

	SET NOCOUNT ON; 

	--Si hay datos nuevos (inserted) pero no viejos (deleted) -> Es una INSERCIÓN
	IF EXISTS (SELECT 1 FROM inserted) 
	
		AND NOT EXISTS (SELECT 1 FROM deleted) 
	
		INSERT INTO dbo.AuditoriaFacturas (Accion, idFactura) 
		SELECT 'INSERT', idFactura 
		FROM inserted; 
	
	--Si hay datos nuevos (inserted) y también viejos (deleted) -> Es una ACTUALIZACIÓN
	ELSE IF EXISTS (SELECT 1 FROM inserted) 
	
		AND EXISTS (SELECT 1 FROM deleted) 
		
		INSERT INTO dbo.AuditoriaFacturas (Accion, idFactura) 
		SELECT 'UPDATE', idFactura 
		FROM inserted; 
	
	--Si no hay datos nuevos pero sí viejos (deleted) -> Es una ELIMINACIÓN
	ELSE IF NOT EXISTS (SELECT 1 FROM inserted) 
		AND EXISTS (SELECT 1 FROM deleted)

		INSERT INTO dbo.AuditoriaFacturas (Accion, idFactura) 
		SELECT 'DELETE', idFactura 
		FROM deleted;

END;
GO


--Al facturar, cierra el ticket (le pone FechaSalida)

CREATE OR ALTER TRIGGER dbo.trg_Facturas_CerrarTicketCursor ON dbo.Facturas
AFTER INSERT 
AS
BEGIN

	SET NOCOUNT ON; 
	
	DECLARE @idFactura INT, @idTicket INT, @fecha DATE; 

	/*Crea un cursor local de avance rápido ('FAST_FORWARD', optimizado para lectura) 
       que recopila la información de las facturas que acaban de ser ingresadas 
       en la tabla virtual 'inserted'.*/
	DECLARE cur_ins CURSOR LOCAL FAST_FORWARD FOR 
		SELECT idFactura, idTicket, FechaVenta
		FROM inserted; 
	
	OPEN cur_ins; 
	FETCH NEXT FROM cur_ins INTO @idFactura, @idTicket, @fecha;
	
	--Se repite mientras el cursor encuentre registros por procesar (@@FETCH_STATUS = 0 es éxito)
	WHILE @@FETCH_STATUS = 0

	/*ACTUALIZACIÓN DEL TICKET:
           Modifica la tabla 'Tickets' asignándole una 'FechaSalida'. 
           - Usa ISNULL para asegurar que si la fecha de venta es nula, use la fecha actual del sistema.
           - El filtro 'FechaSalida IS NULL' garantiza que solo cierre tickets que aún estaban abiertos.*/
	BEGIN 
		UPDATE dbo.Tickets 
		SET FechaSalida = ISNULL(@fecha, CAST(GETDATE() AS DATE)) 
		WHERE idTicket = @idTicket AND FechaSalida IS NULL; 
		
		IF @@ROWCOUNT > 0 
			INSERT INTO dbo.AuditoriaFacturas (Accion, idFactura) 
			VALUES ('TICKET_CERRADO', @idFactura); 
			
			FETCH NEXT FROM cur_ins INTO @idFactura, @idTicket, @fecha; 
		
	END 
		
	CLOSE cur_ins; 
	--libera los recursos del cursor de la memoria RAM
	DEALLOCATE cur_ins;

END;
GO

-----------------------------------PRUEBAS-----------------------------------------

SELECT TOP 3 V.NumSerie 
FROM dbo.Vehiculos V
WHERE NOT EXISTS (
			SELECT 1 
			FROM dbo.Tickets T 
			WHERE T.NumSerie = V.NumSerie AND T.FechaSalida IS NULL);



SELECT TOP 3 idPersona AS Cliente 
FROM dbo.Clientes;

SELECT TOP 3 idPersona AS Asesor 
FROM dbo.Asesores;

SELECT TOP 3 idServicio 
FROM dbo.Servicios;

DECLARE @t INT, @w INT, @n INT;


-- PRUEBA 1: ticket nuevo: OK
EXEC dbo.sp_RegistrarTicket 
	@idCliente = 1,
	@NumSerie = '3FA888888HHH00008', 
	@idTicketGenerado = @t OUTPUT;
	
SELECT @t AS TicketCreado;
GO


DECLARE @t INT;
-- PRUEBA 2:) mismo vehículo otra vez: debe fallar por el trigger INSTEAD OF y quedar en LogErrores
BEGIN TRY 

	EXEC dbo.sp_RegistrarTicket @idCliente = 1, @NumSerie = '3N1777777GGG00007', @idTicketGenerado = @t OUTPUT;

END TRY 
BEGIN CATCH 
	SELECT ERROR_MESSAGE() AS ErrorMostrado;
END CATCH 


SELECT TOP 1 * 
FROM dbo.LogErrores
ORDER BY IdLog DESC; 

SELECT @@TRANCOUNT AS TransaccionesAbiertas;
GO

select * from Tickets;


--PRUEBA 3:
DECLARE @t INT, @w INT;

-- 2. Creamos primero el ticket y guardamos su ID generado en la variable @t
EXEC dbo.sp_RegistrarTicket 
    @idCliente = 1, 
    @NumSerie = '9BG222222BBB00002', 
    @idTicketGenerado = @t OUTPUT;

SELECT @t AS TicketCreadoParaTrabajo;

-- 3. Se agrega el trabajo usando ese ticket recién creado (@t)
EXEC dbo.sp_AgregarTrabajo 
    @idTicket = @t, 
    @idServicio = 1, 
    @HorasTrabajadas = 2.5, 
    @Descripcion = 'Cambio de aceite', 
    @idTrabajoGenerado = @w OUTPUT;

-- 4. Se ve el ID del trabajo generado para confirmar el éxito
SELECT @w AS TrabajoCreado;

-- d) @@ROWCOUNT

DECLARE @filas INT;

EXEC dbo.sp_AjustarTarifasServicios @Porcentaje = 5,
@idServicio = 1, 
@FilasImpactadas = @filas OUTPUT;

SELECT @filas AS FilasImpactadas;


GO

-- e) cursor: factura el ticket y el trigger con cursor lo cierra
DECLARE @n INT;

EXEC dbo.sp_FacturarTicketsPendientesCursor 
	@idAsesor = 41, 
	@Facturadas = @n OUTPUT;
	
	SELECT @n AS Facturadas;
	
	SELECT TOP 5 * 
	FROM dbo.AuditoriaFacturas 
	ORDER BY IdAuditoria DESC; -- INSERT y TICKET_CERRADOSELECT idTicket, FechaSalida FROM dbo.Tickets WHERE idTicket = @t;

GO

SELECT * FROM Vehiculos;