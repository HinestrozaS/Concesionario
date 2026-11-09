public static class Util
{
    public static async Task<string?> Intentar(Func<Task> accion)
    {
        try
        {
            await accion();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}