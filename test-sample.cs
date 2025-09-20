using System;
using System.Threading.Tasks;

public class UserRepository
{
    public async Task<User> Login(User user)
    {
        var userDetail = await context.Identities.Where(x => x.Username == user.Username).FirstOrDefaultAsync();
        return userDetail;
    }

    public async Task Register(User user)
    {
        var test = "fdsre";
    }

    public void SomeMethod()
    {
        if (app.Environment.IsDevelopment())
        {
            // This should not be flagged as a method
        }
    }
}
