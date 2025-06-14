// (c) Carlos Pineda Guerrero. 2025
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Text;

namespace FunctionApp1
{
    public class compra_articulo
    {
        class ParamCompraArticulo
        {
            public int id_usuario { get; set; }
            public string? token { get; set; }
            public int id_articulo { get; set; }
            public int cantidad { get; set; }
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("compra_articulo")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]
            HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                ParamCompraArticulo? p = JsonConvert.DeserializeObject<ParamCompraArticulo>(body);

                if (p == null || p.token == null) throw new Exception("Datos incompletos");

                // Variables de conexión
                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");
                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";

                using var conexion = new MySqlConnection(cs);
                await conexion.OpenAsync();
                using var trans = await conexion.BeginTransactionAsync();

                try
                {
                    // Validar token
                    var cmdToken = new MySqlCommand("SELECT COUNT(*) FROM usuarios WHERE id_usuario=@id AND token=@token", conexion, (MySqlTransaction)trans);
                    cmdToken.Parameters.AddWithValue("@id", p.id_usuario);
                    cmdToken.Parameters.AddWithValue("@token", p.token);
                    long count = (long)await cmdToken.ExecuteScalarAsync();

                    if (count == 0)
                        return new ObjectResult(new Error("Token inválido")) { StatusCode = 403 };

                    // Validar existencia
                    var cmdStock = new MySqlCommand("SELECT cantidad FROM stock WHERE id_articulo=@id", conexion, (MySqlTransaction)trans);
                    cmdStock.Parameters.AddWithValue("@id", p.id_articulo);
                    object? result = await cmdStock.ExecuteScalarAsync();
                    if (result == null || Convert.ToInt32(result) < p.cantidad)
                        throw new Exception("No hay suficientes artículos");

                    // Insertar o actualizar en carrito_compra (por índice único)
                    var cmdInsert = new MySqlCommand(@"
                        INSERT INTO carrito_compra (id_usuario, id_articulo, cantidad)
                        VALUES (@id_usuario, @id_articulo, @cantidad)
                        ON DUPLICATE KEY UPDATE cantidad = cantidad + VALUES(cantidad)", conexion, (MySqlTransaction)trans);
                    cmdInsert.Parameters.AddWithValue("@id_usuario", p.id_usuario);
                    cmdInsert.Parameters.AddWithValue("@id_articulo", p.id_articulo);
                    cmdInsert.Parameters.AddWithValue("@cantidad", p.cantidad);
                    await cmdInsert.ExecuteNonQueryAsync();

                    // Actualizar existencia
                    var cmdUpdate = new MySqlCommand("UPDATE stock SET cantidad = cantidad - @cantidad WHERE id_articulo=@id", conexion, (MySqlTransaction)trans);
                    cmdUpdate.Parameters.AddWithValue("@cantidad", p.cantidad);
                    cmdUpdate.Parameters.AddWithValue("@id", p.id_articulo);
                    await cmdUpdate.ExecuteNonQueryAsync();

                    await trans.CommitAsync();
                    return new OkObjectResult("Artículo agregado al carrito");
                }
                catch (Exception ex)
                {
                    await trans.RollbackAsync();
                    return new BadRequestObjectResult(new Error(ex.Message));
                }
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(new Error(e.Message));
            }
        }
    }
}
