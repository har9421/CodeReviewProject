using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace testproject
{
    public class badCodeExample
    {
        private string connectionstring;
        private int maxretryattempts = 3;
        private string password = "hardcoded123";
        
        public badCodeExample(string connectionstring)
        {
            this.connectionstring = connectionstring;
        }

        public async Task<bool> ProcessUserAsync(int userId, string username, string email, string phone, string address, string city, string state, string zipcode, string country, string department)
        {
            try
            {
                // Too many parameters - violates coding standards
                var result = await ValidateUserAsync(userId);
                
                // String concatenation instead of StringBuilder
                var message = "Processing user: " + userId + " with name: " + username + " and email: " + email;
                Console.WriteLine(message);
                
                // Magic numbers
                if (userId > 1000)
                {
                    return true;
                }
                
                // Deep nesting
                if (result)
                {
                    if (username != null)
                    {
                        if (email != null)
                        {
                            if (phone != null)
                            {
                                if (address != null)
                                {
                                    return await ProcessUserDataAsync(userId, username, email, phone, address, city, state, zipcode, country, department);
                                }
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                // Generic exception handling without logging
                throw;
            }
        }

        private async Task<bool> ValidateUserAsync(int userId)
        {
            // No input validation
            await Task.Delay(100);
            return true;
        }

        private async Task<bool> ProcessUserDataAsync(int userId, string username, string email, string phone, string address, string city, string state, string zipcode, string country, string department)
        {
            // Another method with too many parameters
            await Task.Delay(50);
            
            // SQL injection vulnerability
            var query = "SELECT * FROM Users WHERE Id = " + userId;
            
            return true;
        }
    }
}

