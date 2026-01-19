using Core.Extensions;
using Core.Utilities.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Authorize]
    public abstract class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Gets the current user's ID from the JWT token
        /// </summary>
        protected Guid CurrentUserId => User.GetUserIdOrThrow();

        /// <summary>
        /// Returns appropriate HTTP response based on the result
        /// </summary>
        /// <param name="result">The result to process</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected IActionResult HandleResult(Core.Utilities.Results.IResult result)
        {
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Returns appropriate HTTP response based on the result with async support
        /// </summary>
        /// <param name="resultTask">The async result to process</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleResultAsync(Task<Core.Utilities.Results.IResult> resultTask)
        {
            var result = await resultTask;
            return HandleResult(result);
        }

        /// <summary>
        /// Returns appropriate HTTP response based on the data result
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="result">The data result to process</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected IActionResult HandleDataResult<T>(IDataResult<T> result)
        {
            return result.Success ? Ok(result) : BadRequest(result);
        }

        /// <summary>
        /// Returns appropriate HTTP response based on the data result with async support
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="resultTask">The async data result to process</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleDataResultAsync<T>(Task<IDataResult<T>> resultTask)
        {
            var result = await resultTask;
            return HandleDataResult(result);
        }

        /// <summary>
        /// Handles common service operations with user ID injection
        /// </summary>
        /// <param name="serviceOperation">The service operation that takes userId as first parameter</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleUserOperation(Func<Guid, Task<Core.Utilities.Results.IResult>> serviceOperation)
        {
            var result = await serviceOperation(CurrentUserId);
            return HandleResult(result);
        }

        /// <summary>
        /// Handles common service operations with user ID injection for data results
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="serviceOperation">The service operation that takes userId as first parameter</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleUserDataOperation<T>(Func<Guid, Task<IDataResult<T>>> serviceOperation)
        {
            var result = await serviceOperation(CurrentUserId);
            return HandleDataResult(result);
        }

        /// <summary>
        /// Handles common CRUD operations for create
        /// </summary>
        /// <typeparam name="TDto">The DTO type</typeparam>
        /// <param name="dto">The DTO to create</param>
        /// <param name="createOperation">The create operation</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleCreateOperation<TDto>(TDto dto, Func<TDto, Guid, Task<Core.Utilities.Results.IResult>> createOperation)
        {
            var result = await createOperation(dto, CurrentUserId);
            return HandleResult(result);
        }

        /// <summary>
        /// Handles common CRUD operations for update
        /// </summary>
        /// <typeparam name="TDto">The DTO type</typeparam>
        /// <param name="dto">The DTO to update</param>
        /// <param name="updateOperation">The update operation</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleUpdateOperation<TDto>(TDto dto, Func<TDto, Guid, Task<Core.Utilities.Results.IResult>> updateOperation)
        {
            var result = await updateOperation(dto, CurrentUserId);
            return HandleResult(result);
        }

        /// <summary>
        /// Handles common CRUD operations for delete
        /// </summary>
        /// <param name="id">The ID to delete</param>
        /// <param name="deleteOperation">The delete operation</param>
        /// <returns>OK if successful, BadRequest if failed</returns>
        protected async Task<IActionResult> HandleDeleteOperation(Guid id, Func<Guid, Guid, Task<Core.Utilities.Results.IResult>> deleteOperation)
        {
            var result = await deleteOperation(id, CurrentUserId);
            return HandleResult(result);
        }
    }
}
