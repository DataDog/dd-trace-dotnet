namespace WebService.Models
{

    public class SecurityAuthenticationRequest
    {
        public string Email { get; set; }

        public string Question { get; set; }

        public string Answer { get; set; }
    }
}
