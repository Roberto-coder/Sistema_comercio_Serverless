// modifica_usuario.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;

namespace Sistema_comercio_Serverless
{
    public class modifica_usuario
    {
        class Usuario
        {
            public string? email;
            public string? nombre;
            public string? apellido_paterno;
            public string? apellido_materno;
            public DateTime? fecha_nacimiento;
            public long? telefono;
            public string? genero;
            public string? password;
            public string? token;
            public string? foto;  // base64
        }

        class ParamModificaUsuario
        {
            public Usuario? usuario;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje) => this.mensaje = mensaje;
        }

        [Function("modifica_usuario")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var data = JsonConvert.DeserializeObject<ParamModificaUsuario>(body);
                if (data?.usuario == null) throw new Exception("Datos de usuario requeridos");

                var u = data.usuario;
                if (string.IsNullOrEmpty(u.email)) throw new Exception("Email requerido");
                if (string.IsNullOrEmpty(u.nombre)) throw new Exception("Nombre requerido");
                if (string.IsNullOrEmpty(u.apellido_paterno)) throw new Exception("Apellido paterno requerido");
                if (u.fecha_nacimiento == null) throw new Exception("Fecha de nacimiento requerida");

                var cs = BuildConnectionString();
                using var conexion = new MySqlConnection(cs);
                conexion.Open();
                using var trans = conexion.BeginTransaction();

                try
                {
                    var cmd = new MySqlCommand(@"
                        UPDATE usuarios SET nombre=@n,apellido_paterno=@ap,apellido_materno=@am,
                        fecha_nacimiento=@fn,telefono=@t,genero=@g,password=@pw,token=@tk
                        WHERE email=@e", conexion, trans);

                    cmd.Parameters.AddWithValue("@n", u.nombre);
                    cmd.Parameters.AddWithValue("@ap", u.apellido_paterno);
                    cmd.Parameters.AddWithValue("@am", u.apellido_materno);
                    cmd.Parameters.AddWithValue("@fn", u.fecha_nacimiento);
                    cmd.Parameters.AddWithValue("@t", u.telefono);
                    cmd.Parameters.AddWithValue("@g", u.genero);
                    cmd.Parameters.AddWithValue("@pw", u.password ?? "");
                    cmd.Parameters.AddWithValue("@tk", u.token ?? "");
                    cmd.Parameters.AddWithValue("@e", u.email);
                    cmd.ExecuteNonQuery();

                    var delFoto = new MySqlCommand(
                        "DELETE FROM fotos_usuarios WHERE id_usuario=(SELECT id_usuario FROM usuarios WHERE email=@e)",
                        conexion, trans);
                    delFoto.Parameters.AddWithValue("@e", u.email);
                    delFoto.ExecuteNonQuery();

                    if (!string.IsNullOrEmpty(u.foto))
                    {
                        var insFoto = new MySqlCommand(
                            "INSERT INTO fotos_usuarios (foto,id_usuario) VALUES (@foto,(SELECT id_usuario FROM usuarios WHERE email=@e))",
                            conexion, trans);
                        insFoto.Parameters.AddWithValue("@foto", Convert.FromBase64String(u.foto));
                        insFoto.Parameters.AddWithValue("@e", u.email);
                        insFoto.ExecuteNonQuery();
                    }

                    trans.Commit();
                    return new OkObjectResult("Usuario modificado correctamente");
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    throw new Exception("Error en modificación: " + ex.Message);
                }
            }
            catch (Exception e)
            {
                return new BadRequestObjectResult(new Error(e.Message));
            }
        }

        private string BuildConnectionString()
        {
            return $"Server={Environment.GetEnvironmentVariable("Server")};" +
                   $"UserID={Environment.GetEnvironmentVariable("UserID")};" +
                   $"Password={Environment.GetEnvironmentVariable("Password")};" +
                   $"Database={Environment.GetEnvironmentVariable("Database")};SslMode=Preferred;";
        }
    }
}
