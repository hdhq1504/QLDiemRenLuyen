namespace QLDiemRenLuyen.Services
{
    public static class IdGenerator
    {
        public static string NewId() => Guid.NewGuid().ToString("N");
    }
}
