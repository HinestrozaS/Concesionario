namespace Concesionario.WEB.Services;

public class ApiException : Exception
{
    public int Numero { get; set; }

    public ApiException(string mensaje)
        : base(mensaje)
    {
    }

    public ApiException(string mensaje, int numero)
        : base(mensaje)
    {
        Numero = numero;
    }
}
