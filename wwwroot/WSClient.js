function WSClient(url) {
	this.url = url;

	this.postUrl = function (operation, args, callback) {
		const request = new XMLHttpRequest();
		let body = "";
		let pairs = [];

		try {
			for (const name in args) {
				let value = args[name];
				if (typeof value !== "string") value = JSON.stringify(value);
				pairs.push(name + '=' + encodeURIComponent(value));
			}
			body = pairs.join('&');

			request.open("POST", `${this.url}/${operation}`);
			request.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
			request.responseType = "json";

			request.onload = function () {
				if (callback) callback(request.status, resolveReferences(request.response));
			};

			request.send(body);
		} catch (e) {
			console.error("Error en postUrl:", e.message);
		}
	};

	this.postJson = function (operation, args, callback) {
		try {
			const request = new XMLHttpRequest();
			const body = JSON.stringify(args);

			request.open("POST", `${this.url}/${operation}`);
			request.setRequestHeader("Content-Type", "application/json");
			request.responseType = "json";

			request.onload = function () {
				if (callback) callback(request.status, request.response);
			};

			request.onerror = function () {
				console.error("Error de red en postJson");
				if (callback) callback(500, { mensaje: "Error de red" });
			};

			request.send(body);
		} catch (e) {
			console.error("Error en postJson:", e.message);
		}
	};
}

// Función auxiliar para resolver referencias circulares (opcional, si usas $ref/$id)
function resolveReferences(json) {
	if (typeof json === "string") json = JSON.parse(json);

	const byid = {}, refs = [];

	json = (function recurse(obj, prop, parent) {
		if (typeof obj !== "object" || !obj) return obj;
		if (Array.isArray(obj)) {
			for (let i = 0; i < obj.length; i++) {
				if (typeof obj[i] !== "object" || !obj[i]) continue;
				else if ("$ref" in obj[i]) obj[i] = recurse(obj[i], i, obj);
				else obj[i] = recurse(obj[i], prop, obj);
			}
			return obj;
		}
		if ("$ref" in obj) {
			const ref = obj["$ref"];
			if (ref in byid) return byid[ref];
			refs.push([parent, prop, ref]);
			return;
		} else if ("$id" in obj) {
			const id = obj["$id"];
			delete obj["$id"];
			for (let key in obj) {
				obj[key] = recurse(obj[key], key, obj);
			}
			byid[id] = obj;
		}
		return obj;
	})(json);

	for (let i = 0; i < refs.length; i++) {
		const ref = refs[i];
		ref[0][ref[1]] = byid[ref[2]];
	}
	return json;
}
