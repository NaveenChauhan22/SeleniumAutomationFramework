using Allure.NUnit;
using Allure.NUnit.Attributes;
using Allure.Net.Commons;
using Framework.API;
using Framework.Reporting;
using Newtonsoft.Json.Linq;

namespace APITests;

[AllureNUnit]
[AllureParentSuite("APITests")]
[AllureSuite("Bookings API")]
[AllureFeature("Bookings")]
public class BookingsAPITests : APITestBase
{
    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.High)]
    [AllureStory("POST/GET/DELETE /api/bookings")]
    [AllureSeverity(SeverityLevel.critical)]
    public async Task CreateLookupAndCancelBooking()
    {
        int eventId = 0;
        int bookingId = 0;

        try
        {
            var eventResponse = await EventsApi.CreateEventAsync(BuildPayload(ApiData.Bookings.SupportingEventPayload));
            APIClient.ValidateStatusCode(eventResponse.StatusCode, 201);
            eventId = ExtractRequiredInt(
                eventResponse.ResponseBody,
                ApiData.Assertions.Bookings.SupportingEventIdJsonPath,
                "Supporting event id was not found in response.");

            var bookingResponse = await BookingsApi.CreateBookingAsync(BuildPayload(
                ApiData.Bookings.CreatePayload,
                new Dictionary<string, JToken>
                {
                    ["eventId"] = JToken.FromObject(eventId)
                }));
            APIClient.ValidateStatusCode(bookingResponse.StatusCode, 201);

            bookingId = ExtractRequiredInt(
                bookingResponse.ResponseBody,
                ApiData.Assertions.Bookings.CreatedBookingIdJsonPath,
                "Created booking id was not found in response.");
            var bookingRef = ExtractRequiredString(
                bookingResponse.ResponseBody,
                ApiData.Assertions.Bookings.BookingReferenceJsonPath,
                "Booking reference was not found in response.");

            var byIdResponse = await BookingsApi.GetBookingByIdAsync(bookingId);
            APIClient.ValidateStatusCode(byIdResponse.StatusCode, 200);

            var byRefResponse = await BookingsApi.GetBookingByReferenceAsync(bookingRef);
            APIClient.ValidateStatusCode(byRefResponse.StatusCode, 200);
        }
        finally
        {
            if (bookingId > 0)
            {
                try
                {
                    var cancelResponse = await BookingsApi.CancelBookingAsync(bookingId);
                    Assert.That((int)cancelResponse.StatusCode, Is.AnyOf(200, 204, 404));
                }
                catch (Exception ex)
                {
                    Logger.Warning("[CLEANUP] Failed to cancel booking {BookingId}. Error: {Error}", bookingId, ex.Message);
                }
            }

            if (eventId > 0)
            {
                try
                {
                    var deleteEventResponse = await EventsApi.DeleteEventAsync(eventId);
                    Assert.That((int)deleteEventResponse.StatusCode, Is.AnyOf(200, 204, 404));
                }
                catch (Exception ex)
                {
                    Logger.Warning("[CLEANUP] Failed to delete event {EventId}. Error: {Error}", eventId, ex.Message);
                }
            }
        }
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("GET /api/bookings with query params")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task ListBookings_ShouldSupportPaginationStatusAndEventFilter()
    {
        int eventId = 0;
        int bookingId = 0;

        try
        {
            var eventResponse = await EventsApi.CreateEventAsync(BuildPayload(ApiData.Bookings.SupportingEventPayload));
            APIClient.ValidateStatusCode(eventResponse.StatusCode, 201);
            eventId = ExtractRequiredInt(
                eventResponse.ResponseBody,
                ApiData.Assertions.Bookings.SupportingEventIdJsonPath,
                "Supporting event id was not found in response.");

            var bookingResponse = await BookingsApi.CreateBookingAsync(BuildPayload(
                ApiData.Bookings.CreatePayload,
                new Dictionary<string, JToken>
                {
                    ["eventId"] = JToken.FromObject(eventId)
                }));
            APIClient.ValidateStatusCode(bookingResponse.StatusCode, 201);
            bookingId = ExtractRequiredInt(
                bookingResponse.ResponseBody,
                ApiData.Assertions.Bookings.CreatedBookingIdJsonPath,
                "Created booking id was not found in response.");

            var listResponse = await BookingsApi.ListBookingsAsync(
                ApiData.Queries.Bookings.Page,
                ApiData.Queries.Bookings.Limit,
                ApiData.Queries.Bookings.Status,
                eventId);

            APIClient.ValidateStatusCode(listResponse.StatusCode, 200);
            Assert.That(listResponse.ResponseBody, Does.Contain(ApiData.Assertions.Bookings.PaginationField));
        }
        finally
        {
            if (bookingId > 0)
            {
                try
                {
                    await BookingsApi.CancelBookingAsync(bookingId);
                }
                catch (Exception ex)
                {
                    Logger.Warning("[CLEANUP] Failed to cancel booking {BookingId}. Error: {Error}", bookingId, ex.Message);
                }
            }

            if (eventId > 0)
            {
                try
                {
                    await EventsApi.DeleteEventAsync(eventId);
                }
                catch (Exception ex)
                {
                    Logger.Warning("[CLEANUP] Failed to delete event {EventId}. Error: {Error}", eventId, ex.Message);
                }
            }
        }
    }

    [Test]
    [Category("High")]
    [Category("Smoke")]
    [Priority(TestPriority.Medium)]
    [AllureStory("POST /api/bookings invalid payload")]
    [AllureSeverity(SeverityLevel.normal)]
    public async Task CreateBooking_WithInvalidPayload_ShouldReturnValidationError()
    {
        int eventId = 0;

        try
        {
            var eventResponse = await EventsApi.CreateEventAsync(BuildPayload(ApiData.Bookings.SupportingEventPayload));
            APIClient.ValidateStatusCode(eventResponse.StatusCode, 201);
            eventId = ExtractRequiredInt(
                eventResponse.ResponseBody,
                ApiData.Assertions.Bookings.SupportingEventIdJsonPath,
                "Supporting event id was not found in response.");

            var invalidPayload = BuildPayload(
                ApiData.Bookings.InvalidCreatePayload,
                new Dictionary<string, JToken>
                {
                    ["eventId"] = JToken.FromObject(eventId)
                });

            var response = await BookingsApi.CreateBookingAsync(invalidPayload);
            APIClient.ValidateStatusCode(response.StatusCode, 400);
            Assert.That(response.ResponseBody, Does.Contain(ApiData.Assertions.Bookings.ValidationErrorField).IgnoreCase);
        }
        finally
        {
            if (eventId > 0)
            {
                try
                {
                    var deleteResponse = await EventsApi.DeleteEventAsync(eventId);
                    Assert.That((int)deleteResponse.StatusCode, Is.AnyOf(200, 204, 404));
                }
                catch (Exception ex)
                {
                    Logger.Warning("[CLEANUP] Failed to delete event {EventId}. Error: {Error}", eventId, ex.Message);
                }
            }
        }
    }
}
