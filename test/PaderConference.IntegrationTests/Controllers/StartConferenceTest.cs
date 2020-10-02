﻿using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using PaderConference.IntegrationTests._Helpers;
using PaderConference.Models.Request;
using PaderConference.Models.Response;
using Xunit;

namespace PaderConference.IntegrationTests.Controllers
{
    public class StartConferenceTest : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public StartConferenceTest(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Run()
        {
            var response = await _client.PostAsync("/api/v1/auth/login",
                new JsonContent(new LoginRequestDto {UserName = "Vincent", Password = "123"}));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.DeserializeJsonObject<LoginResponseDto>();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            response = await _client.PostAsync("/api/v1/conference",
                new JsonContent(new StartConferenceRequestDto()));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var conferenceInfo = await response.Content.DeserializeJsonObject<StartConferenceResponseDto>();
            Assert.NotEmpty(conferenceInfo.ConferenceId);
        }
    }
}