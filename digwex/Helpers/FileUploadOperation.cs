using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace Digwex.Helpers
{
  public class FileUploadOperation : IOperationFilter
  {
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
      if (operation.OperationId == "upload") {
        operation.Parameters.Add(
                    new OpenApiParameter {
                      Name = "formFile",
                      In = ParameterLocation.Header,
                      Description = "Upload File",
                      Required = true,
                      Schema = new OpenApiSchema {
                        Type = "file",
                        Format = "binary"
                      }
                    });
      }
    }
  }
}
