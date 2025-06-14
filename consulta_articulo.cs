using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace FunctionApp1
{
    public class consulta_articulos
    {
        class ParamConsulta
        {
            public int id_usuario;
            public string? token;
            public string? palabra_clave;
        }

        class Articulo
        {
            public int id_articulo;
            public string? nombre;
            public string? descripcion;
            public decimal precio;
            public int existencia;
            public string? fotografia;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("consulta_articulos")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                ParamConsulta? data = JsonConvert.DeserializeObject<ParamConsulta>(body);

                if (data == null || data.id_usuario <= 0 || string.IsNullOrEmpty(data.token))
                    throw new Exception("Faltan parámetros");

                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");

                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";
                using var conexion = new MySqlConnection(cs);
                conexion.Open();

                // Validación de token
                var cmd_valida = new MySqlCommand(
                    "SELECT COUNT(*) FROM usuarios WHERE id_usuario=@id AND token=@token", conexion);
                cmd_valida.Parameters.AddWithValue("@id", data.id_usuario);
                cmd_valida.Parameters.AddWithValue("@token", data.token);
                int valido = Convert.ToInt32(cmd_valida.ExecuteScalar());
                if (valido == 0) throw new Exception("Token inválido");

                // Consulta con LIKE en nombre o descripción
                var cmd = new MySqlCommand(@"
                    SELECT s.id_articulo, s.nombre, s.descripcion, s.precio, s.cantidad,
                           f.fotografia, LENGTH(f.fotografia)
                    FROM stock s
                    LEFT JOIN fotos_articulos f ON s.id_articulo = f.id_articulo
                    WHERE s.nombre LIKE @clave OR s.descripcion LIKE @clave", conexion);

                string clave = "%" + (data.palabra_clave ?? "") + "%";
                cmd.Parameters.AddWithValue("@clave", clave);

                var articulos = new List<Articulo>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var a = new Articulo
                    {
                        id_articulo = reader.GetInt32(0),
                        nombre = reader.GetString(1),
                        descripcion = !reader.IsDBNull(2) ? reader.GetString(2) : "",
                        precio = reader.GetDecimal(3),
                        existencia = reader.GetInt32(4)
                    };

                    if (!reader.IsDBNull(5))
                    {
                        int len = reader.GetInt32(6);
                        byte[] foto = new byte[len];
                        reader.GetBytes(5, 0, foto, 0, len);
                        a.fotografia = Convert.ToBase64String(foto);
                    }

                    articulos.Add(a);
                }

                return new OkObjectResult(JsonConvert.SerializeObject(articulos));
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(e.Message)));
            }
        }
    }
}
