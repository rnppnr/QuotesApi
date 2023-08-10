using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using QuotesApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace QuotesApi
{
    public class Quotes
    {
        private readonly ILogger<Quotes> _logger;
        private readonly int _max;
        private readonly Random _random = new Random();
        private readonly QuotesContext _dbContext;

        /// <summary>
        /// Constructor for the Quotes class.
        /// </summary>
        /// <param name="log">The logger used for logging messages related to the Quotes class.</param>
        public Quotes(ILogger<Quotes> log)
        {
            // Initialize the logger for the Quotes class.
            _logger = log;

            // Create an instance of QuotesContext to interact with the Quotes database.
            _dbContext = new QuotesContext();

            // Calculate the maximum value of quote IDs in the database.
            // This value is used for generating new quote IDs or for random selection.
            _max = _dbContext.Quote.DefaultIfEmpty().Max(q => q == null ? 0 : q.Id);
        }

        [FunctionName("GetQuote")]
        [OpenApiOperation(operationId: "Get", tags: new[] { "Quotes" })]
        [OpenApiParameter(name: "author", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "The author of the quote.")]
        [OpenApiParameter(name: "maxLength", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "The maximum lenngth of the quote.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(Quote), Description = "The OK response")]
        /// <summary>
        /// Handles a GET request to retrieve a generated Quote based on provided query parameters.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <returns>An IActionResult representing the result of the retrieval operation.</returns>        
        public IActionResult GetQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a GET request.");

            // Call the GenerateQuote method to generate a quote based on query parameters.
            // The "author" and "maxLength" query parameters are extracted from the request.
            // The GenerateQuote method filters and selects a quote based on these parameters.
            // Return the generated quote in an Ok response.
            return new OkObjectResult(GenerateQuote(req.Query["author"], req.Query["maxLength"]));
        }


        [FunctionName("GetQuoteById")]
        [OpenApiOperation(operationId: "GetById", tags: new[] { "Quotes" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "The Id of the quote to be returned.")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(Quote), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "text/string", bodyType: typeof(string), Description = "The Not Found response")]
        /// <summary>
        /// Handles a GET request to retrieve a Quote by its ID.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="id">The ID of the Quote to be retrieved.</param>
        /// <returns>An IActionResult representing the result of the retrieval operation.</returns>        
        public IActionResult GetQuoteById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "quotes/{id}")] HttpRequest req, int id)
        {
            _logger.LogInformation("C# HTTP trigger function processed a GET request.");

            // Find the Quote in the database using the provided ID.
            Quote quote = _dbContext.Quote.Find(id);

            // If the Quote is not found, return a NotFound response.
            if (quote == null)
            {
                return new NotFoundObjectResult($"Quote with id {id} not found");
            }

            // Return an Ok response with the retrieved Quote.
            return new OkObjectResult(quote);
        }


        [FunctionName("CreateQuote")]
        [OpenApiOperation(operationId: "Create", tags: new[] { "Quotes" })]
        [OpenApiRequestBody(contentType: "text/json", bodyType: typeof(Create), Required = true, Description = "Quote that is to be added to the store")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "text/json", bodyType: typeof(Quote), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/string", bodyType: typeof(string), Description = "The Bad Request response")]
        /// Handles a POST request to create a new Quote.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <returns>An IActionResult representing the result of the create operation.</returns>        
        public async Task<IActionResult> CreateQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "quotes/create")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a POST request.");

            // Read the request body and deserialize it into a dynamic object.
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // Extract author and text from the deserialized data.
            string author = data?.author;
            string text  = data?.text;

            // Initialize a default response message for potential errors.
            string responseMessage = "Error: quote was not created";

            try
            {
                // Check if both author and text are provided before proceeding.
                if (!string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(text)) 
                {
                    // Create a new Quote using the provided author and text.
                    Quote quote = await CreateQuote(author, text);

                    // Update response message and return a 201 Created response.
                    responseMessage = "Success: quote was created";
                    return new ObjectResult(quote) { StatusCode = StatusCodes.Status201Created };
                }
            } 
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return new ObjectResult("Server Error") { StatusCode = StatusCodes.Status500InternalServerError };
            }

            // Return a BadRequest response with the appropriate message.
            return new BadRequestObjectResult(responseMessage);
        }


        [FunctionName("DeleteQuote")]
        [OpenApiOperation(operationId: "Delete", tags: new[] { "Quotes" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "The Id of the quote to be deleted.")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Indicates success. Returns no payload")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "text/string", bodyType: typeof(string), Description = "The Bad Request response")]
        /// <summary>
        /// Handles a DELETE request to remove a Quote identified by its ID.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="id">The ID of the Quote to be deleted.</param>
        /// <returns>An IActionResult representing the result of the delete operation.</returns>        
        public async Task<IActionResult> DeleteQuote (
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "quotes/delete/{id}")] HttpRequest req, int id)
        {
            _logger.LogInformation("C# HTTP trigger function processed a DELETE request.");

            try
            {
                // Find the Quote in the database using the provided ID.
                Quote quote = _dbContext.Quote.Find(id);

                // Remove the Quote from the database.
                var result = _dbContext.Quote.Remove(quote);

                // Save the changes to the database.
                await _dbContext.SaveChangesAsync();

                // Return a 204 No Content response indicating successful deletion.
                return new ObjectResult(null) { StatusCode = StatusCodes.Status204NoContent };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return new ObjectResult("Server Error") { StatusCode = StatusCodes.Status500InternalServerError };
            }
        }


        [FunctionName("UpdateQuote")]
        [OpenApiOperation(operationId: "Update", tags: new[] { "Quotes" })]
        [OpenApiParameter(name: "id", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "The Id of the quote to be deleted.")]
        [OpenApiRequestBody(contentType: "text/json", bodyType: typeof(Create), Required = true, Description = "Quote object that needs to be added to the store")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(Quote), Description = "The OK response")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "text/string", bodyType: typeof(string), Description = "The Not Found response")]
        /// <summary>
        /// Handles a PATCH request to update a Quote identified by its ID.
        /// </summary>
        /// <param name="req">The incoming HTTP request.</param>
        /// <param name="id">The ID of the Quote to be updated.</param>
        /// <returns>An IActionResult representing the result of the update operation.</returns>
        public async Task<IActionResult> UpdateQuote(
            [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "quotes/update/{id}")] HttpRequest req, int id)
        {
            _logger.LogInformation("C# HTTP trigger function processed a PATCH request.");

            // Read the request body and deserialize it into a dynamic object.
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // Extract author and text from the deserialized data.
            string author = data?.author;
            string text = data?.text;

            // Find the Quote in the database using the provided ID.
            Quote quote = _dbContext.Quote.Find(id);

            // If the Quote is not found, return a NotFound response.
            if (quote == null)
            {
                return new NotFoundObjectResult("Quote not found");
            }

            // Update the Quote's author and text if provided in the request data.
            if (!string.IsNullOrEmpty(author)) quote.Author = author;
            if (!string.IsNullOrEmpty(text)) quote.Text = text;

            // Save the changes to the database.
            await _dbContext.SaveChangesAsync();

            // Return an Ok response with the updated Quote.
            return new OkObjectResult(quote);
        }

        /// <summary>
        /// Generates a Quote based on provided criteria from the database.
        /// </summary>
        /// <param name="authorName">The author's name to filter quotes by (optional).</param>
        /// <param name="maxLength">The maximum length of quotes to consider (optional).</param>
        /// <returns>A randomly selected Quote object or null if none match the criteria.</returns>
        private Quote GenerateQuote(string authorName, string maxLength)
        {
            Quote quote = null;
            List<Quote> quotes = null;

            // Determine if author and maximum length filters are provided.
            bool hasAuthor = !string.IsNullOrEmpty(authorName);
            bool hasLength = !string.IsNullOrEmpty(maxLength);
            
            int.TryParse(maxLength, out int length);
            int id;

            // Filter quotes based on author and length conditions.
            if (hasAuthor && length > 0)
            {
                quotes = _dbContext.Quote.Where(q => q.Author == authorName && q.Length <= length).ToList();
            }

            // If quote is still null and author filter is provided, re-filter quotes.
            if (quote == null && hasAuthor)
            {
                quotes = _dbContext.Quote.Where(quote => quote.Author == authorName).ToList();
            }

            // If quote is still null and length filter is provided, re-filter quotes.
            if (quote == null && length > 0)
            {
                quotes = _dbContext.Quote.Where(quote => quote.Length <= length).ToList();
            }

            // If any matching quotes are found, randomly select one.
            if (quotes != null)
            {
                id = _random.Next(0, quotes.Count);
                quote = quotes[id];
            }

            // If quote is still null, randomly select a quote by ID.
            if (quote == null)
            {
                id = _random.Next(0, _max);
                quote = _dbContext.Quote.FirstOrDefault(quote => quote.Id == id);
            }

            // Return the generated quote (or null if no matching quotes found).
            return quote;
        }

        /// <summary>
        /// Creates a new Quote in the database with the provided author and text.
        /// </summary>
        /// <param name="author">The author of the quote.</param>
        /// <param name="text">The text of the quote.</param>
        /// <returns>The newly created Quote object.</returns>
        private async Task<Quote> CreateQuote(string author, string text)
        {
            Quote quote = new Quote() { Author = author, Text = text, Length = text.Length };

            var result = await _dbContext.Quote.AddAsync(quote);

            await _dbContext.SaveChangesAsync();

            return quote;
        }

    }
}

