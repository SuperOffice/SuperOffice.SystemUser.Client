using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using SuperOffice.SystemUser;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Test.SystemUserClient
{
    [TestClass]
    public class SystemUserClientTests
    {
        private readonly IConfiguration _configuration;

        public SystemUserClientTests()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory()) // Ensure it's pointing to the correct directory
                .AddJsonFile($"appsettings.json");

            _configuration = builder.Build();
        }

        [TestMethod]
        public void Test_Configuration()
        {
            var requiredKeys = new[] { "ClientSecret", "ContextIdentifier", "SubDomain", "SystemUserToken", "PrivateKeyFile" };

            foreach (var key in requiredKeys)
            {
                Assert.IsNotNull(_configuration[key], $"{key} is missing in the configuration.");
            }

            var certificate = PrivateCertificate;
            Assert.IsNotNull(certificate, "Private key is missing.");
        }

        private string PrivateCertificate
        {
            get
            {
                // Load the private key from the specified file path
                if (File.Exists(_configuration["PrivateKeyFile"]))
                {
                    return File.ReadAllText(_configuration["PrivateKeyFile"]);
                }
                else
                {
                    throw new FileNotFoundException("Private key file not found.");
                }
            }
        }

        [TestMethod]
        public async Task Test_Get_SystemUser_Ticket_Async()
        {
            var client = new SuperOffice.SystemUser.SystemUserClient(GetSystemUserInfo(), new System.Net.Http.HttpClient());

            var ticket = await client.GetSystemUserTicketAsync();
            Debug.WriteLine("Output: " + ticket);

            StringAssert.StartsWith(ticket, "7T:");
        }

        [TestMethod]
        public void Test_Get_SystemUser_Ticket_Sync()
        {
            var client = new SuperOffice.SystemUser.SystemUserClient(GetSystemUserInfo(), new System.Net.Http.HttpClient());

            var ticket = client.GetSystemUserTicketAsync().GetAwaiter().GetResult();
            Debug.WriteLine("Output: " + ticket);

            StringAssert.StartsWith(ticket, "7T:");
        }

        [TestMethod]
        public async Task Test_Validate_SystemUser_JWT_Async()
        {
            var client = new SuperOffice.SystemUser.SystemUserClient(GetSystemUserInfo(), new System.Net.Http.HttpClient());
            var tokenResult = await client.GetSystemUserJwtAsync();
            var tokenValidationResult = await client.ValidateSystemUserResultAsync(tokenResult);
            Assert.IsTrue(tokenValidationResult.IsValid);
        }

        [TestMethod]
        [ExpectedException(typeof(HttpRequestException))]
        public async Task When_Missing_SystemUserInfo_Throw_HttpRequestException()
        {
            var client = new SuperOffice.SystemUser.SystemUserClient(GetWrongSystemUserInfo(), new System.Net.Http.HttpClient());
            var tokenResult = await client.GetSystemUserJwtAsync();
        }


        [TestMethod]
        [ExpectedException(typeof(SuperOffice.SystemUser.Client.Exceptions.InvalidSubDomainException))]
        public async Task When_SubDomain_Explicitly_Set_Throw_InvalidSubDomainException()
        {
            var sysUser = GetSystemUserInfo();
            sysUser.SubDomain = "production";
            var client = new SuperOffice.SystemUser.SystemUserClient(GetSystemUserInfo(), new System.Net.Http.HttpClient());
            var ticket = await client.GetSystemUserTicketAsync();

        }

        [TestMethod]
        [ExpectedException(typeof(SuperOffice.SystemUser.Client.Exceptions.InvalidSubDomainException))]
        public async Task When_Validating_Token_With_Invalid_SubDomain_Throw_InvalidSubDomainException()
        {
            var client = new SuperOffice.SystemUser.SystemUserClient(GetSystemUserInfo(), new System.Net.Http.HttpClient());
            var token = await client.GetSystemUserJwtAsync();

            var handler = new SuperOffice.SystemUser.Tokens.SystemUserTokenHandler(
                new System.Net.Http.HttpClient(), // HttpClient instance.
                "staging"                       // target online subdomain (sod, qaonline or online)
            );

            var tokenValidationResult = await handler.ValidateAsync(token.Token);
        }

        private SuperOffice.SystemUser.SystemUserInfo GetSystemUserInfo()
        {
            var sysUser = new SuperOffice.SystemUser.SystemUserInfo();
            sysUser.ClientSecret = _configuration["ClientSecret"];
            sysUser.ContextIdentifier = _configuration["ContextIdentifier"];
            sysUser.SubDomain = _configuration["SubDomain"];
            sysUser.SystemUserToken = _configuration["SystemUserToken"];
            sysUser.PrivateKey = PrivateCertificate;

            return sysUser;
        }

        private SuperOffice.SystemUser.SystemUserInfo GetWrongSystemUserInfo()
        {
            var sysUser = new SuperOffice.SystemUser.SystemUserInfo();
            sysUser.ClientSecret = _configuration["ClientSecret"];
            sysUser.ContextIdentifier = _configuration["ContextIdentifier"];
            sysUser.SubDomain = _configuration["SubDomain"];
            sysUser.SystemUserToken = "abc";
            sysUser.PrivateKey = PrivateCertificate;

            return sysUser;
        }
    }
}
