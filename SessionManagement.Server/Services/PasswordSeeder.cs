namespace SessionManagement.Server.Services
{
    public static class PasswordSeeder
    {
        public static void PrintHashes()
        {
            string adminHash    = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            string customerHash = BCrypt.Net.BCrypt.HashPassword("Customer@123");

            Console.WriteLine("=== BCrypt Password Hashes ===");
            Console.WriteLine($"Admin@123    => {adminHash}");
            Console.WriteLine($"Customer@123 => {customerHash}");
            Console.WriteLine("==============================");
        }
    }
}
