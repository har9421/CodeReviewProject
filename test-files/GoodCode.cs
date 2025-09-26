using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestProject
{
    /// <summary>
    /// A well-structured class that follows coding standards
    /// </summary>
    public class GoodCodeExample
    {
        private readonly string _connectionString;
        private const int MaxRetryAttempts = 3;

        /// <summary>
        /// Initializes a new instance of the GoodCodeExample class
        /// </summary>
        /// <param name="connectionString">Database connection string</param>
        public GoodCodeExample(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Processes user data asynchronously
        /// </summary>
        /// <param name="userId">The user identifier</param>
        /// <returns>Task representing the async operation</returns>
        public async Task<bool> ProcessUserAsync(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("User ID must be positive", nameof(userId));
            }

            try
            {
                var result = await ValidateUserAsync(userId);
                return result;
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error processing user {userId}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ValidateUserAsync(int userId)
        {
            // Simulate async operation
            await Task.Delay(100);
            return userId > 0;
        }
    }
}

