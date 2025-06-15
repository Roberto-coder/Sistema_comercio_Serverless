using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FunctionApp1
{
    public class alta_usuario
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
            public string? foto;
            public string? password;
        }

        class ParamAltaUsuario
        {
            public Usuario? usuario;
        }

        class Error
        {
            public string mensaje;
            public Error(string mensaje)
            {
                this.mensaje = mensaje;
            }
        }

        private string GeneraToken()
        {
            return Guid.NewGuid().ToString().Replace("-", "").Substring(0, 20);
        }

        [Function("alta_usuario")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequest req)
        {
            try
            {
                string body = await new StreamReader(req.Body).ReadToEndAsync();
                ParamAltaUsuario? data = JsonConvert.DeserializeObject<ParamAltaUsuario>(body);

                if (data == null || data.usuario == null)
                    throw new Exception("Se esperan los datos del usuario");

                Usuario usuario = data.usuario;

                if (string.IsNullOrEmpty(usuario.email))
                    throw new Exception("Se debe ingresar el email");
                if (string.IsNullOrEmpty(usuario.nombre))
                    throw new Exception("Se debe ingresar el nombre");
                if (string.IsNullOrEmpty(usuario.apellido_paterno))
                    throw new Exception("Se debe ingresar el apellido paterno");
                if (usuario.fecha_nacimiento == null)
                    throw new Exception("Se debe ingresar la fecha de nacimiento");
                if (string.IsNullOrEmpty(usuario.password))
                    throw new Exception("Se debe ingresar la contraseña");

                string? Server = Environment.GetEnvironmentVariable("Server");
                string? UserID = Environment.GetEnvironmentVariable("UserID");
                string? Password = Environment.GetEnvironmentVariable("Password");
                string? Database = Environment.GetEnvironmentVariable("Database");

                string cs = $"Server={Server};UserID={UserID};Password={Password};Database={Database};SslMode=Preferred;";
                using var conexion = new MySqlConnection(cs);
                conexion.Open();

                using var transaccion = conexion.BeginTransaction();
                try
                {
                    string token = GeneraToken();
                    var cmd = new MySqlCommand(@"
                        INSERT INTO usuarios(id_usuario, email, nombre, apellido_paterno, apellido_materno, fecha_nacimiento, telefono, genero, password, token) 
                        VALUES (0, @e, @n, @ap, @am, @f, @t, @g, @p, @token)", conexion, transaccion);

                    cmd.Parameters.AddWithValue("@e", usuario.email);
                    cmd.Parameters.AddWithValue("@n", usuario.nombre);
                    cmd.Parameters.AddWithValue("@ap", usuario.apellido_paterno);
                    cmd.Parameters.AddWithValue("@am", usuario.apellido_materno);
                    cmd.Parameters.AddWithValue("@f", usuario.fecha_nacimiento);
                    cmd.Parameters.AddWithValue("@t", usuario.telefono);
                    cmd.Parameters.AddWithValue("@g", usuario.genero);
                    cmd.Parameters.AddWithValue("@p", usuario.password);
                    cmd.Parameters.AddWithValue("@token", token);
                    cmd.ExecuteNonQuery();

                    long id_usuario = cmd.LastInsertedId;

                    if (usuario.foto != null)
                    {
                        var cmd_foto = new MySqlCommand("INSERT INTO fotos_usuarios (foto,id_usuario) VALUES (@foto,@id_usuario)", conexion, transaccion);
                        cmd_foto.Parameters.AddWithValue("@foto", Convert.FromBase64String(usuario.foto));
                        cmd_foto.Parameters.AddWithValue("@id_usuario", id_usuario);
                        cmd_foto.ExecuteNonQuery();
                    }

                    transaccion.Commit();

                    return new OkObjectResult(JsonConvert.SerializeObject(new
                    {
                        mensaje = "Usuario creado correctamente",
                        id_usuario = id_usuario,
                        token = token
                    }));
                }
                catch (Exception ex)
                {
                    transaccion.Rollback();
                    throw new Exception(ex.Message);
                }
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(JsonConvert.SerializeObject(new Error(ex.Message)));
            }
        }
    }
}
