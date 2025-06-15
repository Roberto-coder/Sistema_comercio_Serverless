using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace FunctionApp1
{
    public class elimina_articulo
    {
        class ParamEliminarArticulo
        {
            public int id_usuario;
            public int id_articulo;
            public string? token;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("elimina_articulo_carrito_compra")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ParamEliminarArticulo>(body);

                if (data == null || data.id_usuario <= 0 || data.id_articulo <= 0 || string.IsNullOrEmpty(data.token))
                    throw new Exception("Datos incompletos");

                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");
                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";

                using var conexion = new MySqlConnection(cs);
                conexion.Open();

                // Validar token
                var cmd_validar = new MySqlCommand(
                    "SELECT COUNT(*) FROM usuarios WHERE id_usuario=@id AND token=@token", conexion);
                cmd_validar.Parameters.AddWithValue("@id", data.id_usuario);
                cmd_validar.Parameters.AddWithValue("@token", data.token);
                int existe = Convert.ToInt32(cmd_validar.ExecuteScalar());
                if (existe == 0)
                    throw new Exception("Token inválido");

                using var trans = conexion.BeginTransaction();

                try
                {
                    // Obtener cantidad actual en el carrito
                    var cmd_cantidad = new MySqlCommand(
                        "SELECT cantidad FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo",
                        conexion, trans);
                    cmd_cantidad.Parameters.AddWithValue("@id_usuario", data.id_usuario);
                    cmd_cantidad.Parameters.AddWithValue("@id_articulo", data.id_articulo);
                    object? result = cmd_cantidad.ExecuteScalar();
                    if (result == null) throw new Exception("El artículo no está en el carrito");

                    int cantidad = Convert.ToInt32(result);

                    // Sumar cantidad al stock
                    var cmd_update_stock = new MySqlCommand(
                        "UPDATE stock SET cantidad = cantidad + @cantidad WHERE id_articulo = @id_articulo",
                        conexion, trans);
                    cmd_update_stock.Parameters.AddWithValue("@cantidad", cantidad);
                    cmd_update_stock.Parameters.AddWithValue("@id_articulo", data.id_articulo);
                    cmd_update_stock.ExecuteNonQuery();

                    // Eliminar del carrito
                    var cmd_delete = new MySqlCommand(
                        "DELETE FROM carrito_compra WHERE id_usuario=@id_usuario AND id_articulo=@id_articulo",
                        conexion, trans);
                    cmd_delete.Parameters.AddWithValue("@id_usuario", data.id_usuario);
                    cmd_delete.Parameters.AddWithValue("@id_articulo", data.id_articulo);
                    cmd_delete.ExecuteNonQuery();

                    trans.Commit();
                    return new OkObjectResult("Artículo eliminado del carrito");
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    throw new Exception(ex.Message);
                }
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(e.Message)));
            }
        }
    }
}
