﻿using Data.Contexts;
using Data.Models.Domain;
using Data.Models.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using API.Services;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly GoodiesDataContext _context;
        private readonly GoogleService _googleService;

        public CustomersController(GoodiesDataContext context, GoogleService googleService)
        {
            _context = context;
            _googleService = googleService;
        }

        // GET: api/Customers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            return await _context.Customers.ToListAsync();
        }

        // GET: api/Customers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Customer>> GetCustomer(Guid id)
        {
            var customer = await _context.Customers.FindAsync(id);

            if (customer == null)
            {
                return NotFound();
            }

            return customer;
        }

        // PUT: api/Customers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomer(Guid id, Customer customer)
        {
            var existingCustomer = await _context.Customers.FindAsync(id);
            if (existingCustomer == null)
            {
                return NotFound("Customer not found.");
            }

            if (id != existingCustomer.Id)
            {
                return BadRequest("Invalid customer ID");
            }

            try
            {
                existingCustomer.FirstName = customer.FirstName;
                existingCustomer.LastName = customer.LastName;
                existingCustomer.Username = customer.Username;
                existingCustomer.Email = customer.Email;
                existingCustomer.Address = customer.Address;
                existingCustomer.City = customer.City;
                existingCustomer.PostalCode = customer.PostalCode;
                existingCustomer.Province = customer.Province;
                existingCustomer.PhoneNumber = customer.PhoneNumber;
                existingCustomer.ProfilePicture = customer.ProfilePicture;

                _context.Entry(existingCustomer).State = EntityState.Modified;

                await _context.SaveChangesAsync();

                return Ok("Customer updated.");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(id))
                {
                    return NotFound("Customer not found.");
                }
                else
                {
                    throw;
                }
            }
        }


        // DELETE: api/Customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(Guid id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            return Ok("Customer deleted.");
        }

        private bool CustomerExists(Guid id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(CustomerRegisterRequest request)
        {
            var status = new Status();
            if (!ModelState.IsValid)
            {
                status.StatusCode = 0;
                status.Message = "Please pass all the required fields";
                return Ok(status);
            }

            if (_context.Customers.Any(c => c.Username == request.Username))
            {
                status.StatusCode = 0;
                status.Message = "Invalid username.";
                return Ok(status);
            }

            CreatePasswordHash(request.Password,
                 out byte[] passwordHash,
                 out byte[] passwordSalt);

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = request.FirstName,
                LastName = request.LastName,
                Username = request.Username,
                Email = request.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                PhoneNumber = request.PhoneNumber,
                Address = request.Address,
                City = request.City,
                Province = request.Province,
                PostalCode = request.PostalCode,
                Country = request.Country,
                ProfilePicture = request.ProfilePicture,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                VerificationToken = CreateRandomToken()
            };


            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            status.StatusCode = 1;
            status.Message = "Customer successfully registered! :D";
            return Ok(status);

        }


        [HttpPost("login")]
        public async Task<IActionResult> Login(CustomerLoginRequest request)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == request.Username);
            if (customer == null)
            {
                return Ok(new CustomerLoginResponse
                {
                    StatusCode = 0,
                    Message = "Customer not found.",
                });
            }

            if (!VerifyPasswordHash(request.Password, customer.PasswordHash, customer.PasswordSalt))
            {
                return Ok(new CustomerLoginResponse
                {
                    StatusCode = 0,
                    Message = "Invalid password.",
                });
            }

            //if (customer.VerifiedAt == null)
            //{
            //    return BadRequest("Not verified!");
            //}

            return Ok(new CustomerLoginResponse
            {
                Id = customer.Id,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Username = customer.Username,
                Email = customer.Email,
                ProfilePicture = customer.ProfilePicture,
                PhoneNumber = customer.PhoneNumber,
                Address = customer.Address,
                City = customer.City,
                Province = customer.Province,
                PostalCode = customer.PostalCode,
                Country = customer.Country,
                IsCustomer = customer.IsCustomer,
                StatusCode = 1,
                Message = "Customer successfully logged in! :D",
            });
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request)
        {
            var payload = await _googleService.Verify(request.GoogleIdToken);
            Console.WriteLine("Google Payload:", payload);

            if (payload == null)
            {
                return BadRequest("Invalid google token.");
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == payload.Email);
            if (customer == null)
            {
                customer = new Customer
                {
                    Id = Guid.NewGuid(),
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName,
                    Username = payload.Email.Split('@')[0],
                    Email = payload.Email,
                    ProfilePicture = payload.Picture,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    VerificationToken = CreateRandomToken()
                    // Message = "Customer successfully registered! :D",
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            return Ok(new CustomerLoginResponse
            {
                Id = customer.Id,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Username = customer.Username,
                Email = customer.Email,
                ProfilePicture = customer.ProfilePicture,
                PhoneNumber = customer.PhoneNumber,
                Address = customer.Address,
                City = customer.City,
                Province = customer.Province,
                PostalCode = customer.PostalCode,
                Country = customer.Country,
                IsCustomer = customer.IsCustomer,
                StatusCode = 1,
                Message = "Customer successfully logged in! :D",
            });
        }

        // [HttpPost("verify")]
        // public async Task<IActionResult> Verify(string token)
        // {
        //     var customer = await _context.Customers.FirstOrDefaultAsync(c => c.VerificationToken == token);
        //     if (customer == null)
        //     {
        //         return BadRequest("Invalid token.");
        //     }

        //     customer.VerifiedAt = DateTime.Now;
        //     await _context.SaveChangesAsync();

        //     return Ok("Customer verified! :)");
        // }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Username == request.Username);
            if (customer == null)
            {
                return Ok("Customer not found.");
            }

            customer.PasswordResetToken = CreateRandomToken();
            customer.ResetTokenExpires = DateTime.Now.AddDays(1);


            await _context.SaveChangesAsync();

            return Ok(
                new ForgotPasswordResponse
                {
                    StatusCode = 1,
                    PasswordResetToken = customer.PasswordResetToken,
                    Message = "You may now reset your password. :)",
                    Username = customer.Username
                }
            );
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.PasswordResetToken == request.Token);
            if (customer == null || customer.ResetTokenExpires < DateTime.Now)
            {
                return BadRequest("Invalid Token.");
            }

            CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            customer.PasswordHash = passwordHash;
            customer.PasswordSalt = passwordSalt;
            customer.PasswordResetToken = null;
            customer.ResetTokenExpires = null;

            await _context.SaveChangesAsync();

            return Ok(new ResetPasswordResponse
            {
                StatusCode = 1,
                Message = "Password successfully reset. :D",
                Username = customer.Username
            });
        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac
                    .ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computedHash = hmac
                    .ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(passwordHash);
            }
        }

        private string CreateRandomToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        }

    }
}
