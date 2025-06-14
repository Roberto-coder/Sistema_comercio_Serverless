using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Net;

namespace FunctionApp1
{
    public class consulta_usuario
    {
        class Usuario
        {
            public int? id_usuario;
            public string? email;
            public string? nombre;
            public string? apellido_paterno;
            public string? apellido_materno;
            public DateTime? fecha_nacimiento;
            public long? telefono;
            public string? genero;
            public string? foto;
            public string? password;
            public string? token;
        }

        class ParamConsultaUsuario
        {
            public string? email;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) { this.mensaje = mensaje; }
        }

        [Function("consulta_usuario")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")]
            HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                ParamConsultaUsuario? data = JsonConvert.DeserializeObject<ParamConsultaUsuario>(body);
                if (data == null || data.email == null) throw new Exception("Falta email");

                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");

                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";
                using var conexion = new MySqlConnection(cs);
                conexion.Open();

                var cmd = new MySqlCommand(@"
                    SELECT a.id_usuario,a.email,a.nombre,a.apellido_paterno,a.apellido_materno,
                           a.fecha_nacimiento,a.telefono,a.genero,a.password,a.token,b.foto,LENGTH(b.foto)
                    FROM usuarios a LEFT JOIN fotos_usuarios b ON a.id_usuario=b.id_usuario
                    WHERE a.email=@correo", conexion);
                cmd.Parameters.AddWithValue("@correo", data.email);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read()) throw new Exception("El usuario no existe");

                Usuario u = new Usuario();
                u.id_usuario = reader.GetInt32(0);
                u.email = reader.GetString(1);
                u.nombre = reader.GetString(2);
                u.apellido_paterno = reader.GetString(3);
                u.apellido_materno = !reader.IsDBNull(4) ? reader.GetString(4) : null;
                u.fecha_nacimiento = reader.GetDateTime(5);
                u.telefono = !reader.IsDBNull(6) ? reader.GetInt64(6) : null;
                u.genero = !reader.IsDBNull(7) ? reader.GetString(7) : null;
                u.password = reader.GetString(8);
                u.token = reader.GetString(9);
                if (!reader.IsDBNull(10))
                {
                    int longitud = reader.GetInt32(11);
                    byte[] foto = new byte[longitud];
                    reader.GetBytes(10, 0, foto, 0, longitud);
                    u.foto = Convert.ToBase64String(foto);
                }

                return new OkObjectResult(JsonConvert.SerializeObject(u));
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(e.Message)));
            }
        }
    }
}
