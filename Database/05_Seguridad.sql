USE Concesionario;
GO

CREATE TABLE dbo.UsuariosSystem ( 
		idUsuario INT IDENTITY(1,1) PRIMARY KEY, 
		Username VARCHAR(50) UNIQUE NOT NULL, 
		Salt VARBINARY(16) NOT NULL, 
		ClaveHash VARBINARY(64) NOT NULL
);
GO



-- 1. REGISTRO SEGURO: Genera una "sal" aleatoria y guarda la contraseña cifrada (Hash)
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarUsuario 
    @User VARCHAR(50), 
    @ClavePlana VARCHAR(100)
AS
BEGIN 
    SET NOCOUNT ON;
    
    -- Crea una "sal" (bytes aleatorios) para evitar que dos usuarios con 
    -- la misma contraseña tengan el mismo hash final.
    DECLARE @salt VARBINARY(16) = CRYPT_GEN_RANDOM(16); 
    
    -- Guarda el usuario, la sal generada y el hash seguro combinando la sal + la contraseña.
    INSERT INTO dbo.UsuariosSystem (Username, Salt, ClaveHash)
    VALUES (@User, @salt, HASHBYTES('SHA2_256', @salt + CAST(@ClavePlana AS VARBINARY(100))));
END;
GO

-- 2. VALIDACIÓN DE USUARIO: Recalcula el hash con la sal guardada y compara (Devuelve 1 o 0)
CREATE OR ALTER PROCEDURE dbo.sp_ValidarUsuario 
    @User VARCHAR(50), 
    @ClavePlana VARCHAR(100)
AS
BEGIN 
    SET NOCOUNT ON; 

    -- Busca si existe una coincidencia exacta recalculando el hash con la sal original del usuario
    SELECT CASE WHEN EXISTS ( 
        SELECT 1 
        FROM dbo.UsuariosSystem 
        WHERE Username = @User AND	
              ClaveHash = HASHBYTES('SHA2_256', Salt + CAST(@ClavePlana AS VARBINARY(100))) 
    ) THEN 1 ELSE 0 END AS Valido;
END;
GO

-- 3. DEMOSTRACIÓN DE HASH: Registra un admin y prueba accesos correctos e incorrectos
EXEC dbo.sp_RegistrarUsuario 'admin', 'Admin123'; -- Registra al usuario

SELECT Username, Salt, ClaveHash FROM dbo.UsuariosSystem; -- Muestra cómo la contraseña queda ilegible (Hash)

EXEC dbo.sp_ValidarUsuario 'admin', 'Admin123'; -- Devuelve 1 (Contraseña correcta)
EXEC dbo.sp_ValidarUsuario 'admin', 'otra';     -- Devuelve 0 (Contraseña incorrecta)
GO

-- 4. DEMOSTRACIÓN DE CIFRADO REVERSIBLE (ENCRYPTBYPASSPHRASE / DECRYPTBYPASSPHRASE)
-- A diferencia del hash (que solo sirve para validar), este método permite cifrar y volver a descifrar un dato usando una frase secreta.
DECLARE @cifrado VARBINARY(MAX) = ENCRYPTBYPASSPHRASE('FraseSecretaDelEquipo', 'Admin123');

SELECT 
    @cifrado AS Cifrado, -- Muestra el texto totalmente ilegible
    CONVERT(VARCHAR(100), DECRYPTBYPASSPHRASE('FraseSecretaDelEquipo', @cifrado)) AS Descifrado; -- Vuelve a revelar la contraseña original
GO