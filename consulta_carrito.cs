using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace FunctionApp1
{
    public class consulta_carrito
    {
        class ParamConsultaCarrito
        {
            public int id_usuario;
            public string? token;
        }

        class ArticuloCarrito
        {
            public int id_articulo;
            public string? nombre;
            public string? descripcion;
            public decimal precio;
            public int cantidad;
            public string? fotografia; // en base64
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("consulta_carrito")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ParamConsultaCarrito>(body);

                if (data == null || data.id_usuario <= 0 || string.IsNullOrEmpty(data.token))
                    throw new Exception("Faltan datos del usuario");

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
                int count = Convert.ToInt32(cmd_validar.ExecuteScalar());
                if (count == 0)
                    throw new Exception("Token inválido o sesión caducada");

                // Consultar el carrito
                var cmd = new MySqlCommand(@"
                    SELECT c.id_articulo, s.nombre, s.descripcion, s.precio, c.cantidad,
                           f.fotografia, LENGTH(f.fotografia)
                    FROM carrito_compra c
                    JOIN stock s ON c.id_articulo = s.id_articulo
                    LEFT JOIN fotos_articulos f ON s.id_articulo = f.id_articulo
                    WHERE c.id_usuario = @id", conexion);
                cmd.Parameters.AddWithValue("@id", data.id_usuario);

                var resultado = new List<ArticuloCarrito>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var a = new ArticuloCarrito
                    {
                        id_articulo = reader.GetInt32(0),
                        nombre = reader.GetString(1),
                        descripcion = !reader.IsDBNull(2) ? reader.GetString(2) : "",
                        precio = reader.GetDecimal(3),
                        cantidad = reader.GetInt32(4),
                        fotografia = null
                    };

                    if (!reader.IsDBNull(5))
                    {
                        int len = reader.GetInt32(6);
                        byte[] foto = new byte[len];
                        reader.GetBytes(5, 0, foto, 0, len);
                        a.fotografia = Convert.ToBase64String(foto);
                    }

                    resultado.Add(a);
                }

                return new OkObjectResult(JsonConvert.SerializeObject(resultado));
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(ex.Message)));
            }
        }
    }
}
