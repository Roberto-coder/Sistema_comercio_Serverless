using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;

namespace FunctionApp1
{
    public class alta_articulo
    {
        class ParamAltaArticulo
        {
            public string? nombre;
            public string? descripcion;
            public decimal? precio;
            public int? existencia;
            public string? fotografia; // base64
            public int? id_usuario;
            public string? token;
        }

        class Error
        {
            public string mensaje;
            public Error(string m) { mensaje = m; }
        }

        [Function("alta_articulo")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var datos = JsonConvert.DeserializeObject<ParamAltaArticulo>(body);
                if (datos == null) throw new Exception("Faltan datos");

                if (string.IsNullOrEmpty(datos.nombre) ||
                    string.IsNullOrEmpty(datos.descripcion) ||
                    datos.precio == null || datos.existencia == null ||
                    datos.id_usuario == null || string.IsNullOrEmpty(datos.token))
                    throw new Exception("Todos los campos obligatorios deben estar presentes");

                // Conexión
                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");
                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";
                using var conexion = new MySqlConnection(cs);
                conexion.Open();

                // Verificar token
                var cmdToken = new MySqlCommand("SELECT COUNT(*) FROM usuarios WHERE id_usuario=@id AND token=@t", conexion);
                cmdToken.Parameters.AddWithValue("@id", datos.id_usuario);
                cmdToken.Parameters.AddWithValue("@t", datos.token);
                long total = (long)cmdToken.ExecuteScalar();
                if (total == 0) throw new Exception("Token inválido");

                // Iniciar transacción
                using var trans = conexion.BeginTransaction();

                try
                {
                    var cmd = new MySqlCommand(
                        "INSERT INTO stock (nombre, descripcion, precio, cantidad) VALUES (@n, @d, @p, @c)",
                        conexion, trans);
                    cmd.Parameters.AddWithValue("@n", datos.nombre);
                    cmd.Parameters.AddWithValue("@d", datos.descripcion);
                    cmd.Parameters.AddWithValue("@p", datos.precio);
                    cmd.Parameters.AddWithValue("@c", datos.existencia);
                    cmd.ExecuteNonQuery();

                    int id_articulo = (int)cmd.LastInsertedId;

                    if (!string.IsNullOrEmpty(datos.fotografia))
                    {
                        var cmdFoto = new MySqlCommand(
                            "INSERT INTO fotos_articulos (fotografia, id_articulo) VALUES (@foto, @id)",
                            conexion, trans);
                        cmdFoto.Parameters.AddWithValue("@foto", Convert.FromBase64String(datos.fotografia));
                        cmdFoto.Parameters.AddWithValue("@id", id_articulo);
                        cmdFoto.ExecuteNonQuery();
                    }

                    trans.Commit();

                    return new OkObjectResult(JsonConvert.SerializeObject(new { mensaje = "Artículo agregado", id_articulo }));
                }
                catch (Exception e)
                {
                    trans.Rollback();
                    throw new Exception("Error en transacción: " + e.Message);
                }
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(ex.Message)));
            }
        }
    }
}
