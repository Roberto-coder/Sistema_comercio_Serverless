using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace FunctionApp1
{
    public class elimina_carrito
    {
        class ParamVaciarCarrito
        {
            public int id_usuario;
            public string? token;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("elimina_carrito_compra")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ParamVaciarCarrito>(body);

                if (data == null || data.id_usuario <= 0 || string.IsNullOrEmpty(data.token))
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
                    // Obtener todos los artículos del carrito
                    var cmd_select = new MySqlCommand(
                        "SELECT id_articulo, cantidad FROM carrito_compra WHERE id_usuario=@id",
                        conexion, trans);
                    cmd_select.Parameters.AddWithValue("@id", data.id_usuario);

                    var articulos = new List<(int id_articulo, int cantidad)>();
                    using var reader = cmd_select.ExecuteReader();
                    while (reader.Read())
                        articulos.Add((reader.GetInt32(0), reader.GetInt32(1)));
                    reader.Close();

                    // Actualizar el stock
                    foreach (var art in articulos)
                    {
                        var cmd_upd = new MySqlCommand(
                            "UPDATE stock SET cantidad = cantidad + @cantidad WHERE id_articulo = @id",
                            conexion, trans);
                        cmd_upd.Parameters.AddWithValue("@cantidad", art.cantidad);
                        cmd_upd.Parameters.AddWithValue("@id", art.id_articulo);
                        cmd_upd.ExecuteNonQuery();
                    }

                    // Borrar todos los artículos del carrito
                    var cmd_del = new MySqlCommand(
                        "DELETE FROM carrito_compra WHERE id_usuario = @id", conexion, trans);
                    cmd_del.Parameters.AddWithValue("@id", data.id_usuario);
                    cmd_del.ExecuteNonQuery();

                    trans.Commit();
                    return new OkObjectResult("Carrito vaciado correctamente");
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
