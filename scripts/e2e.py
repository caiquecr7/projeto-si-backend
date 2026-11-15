import json, random, sys, urllib.request, urllib.error
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5000/api"
SENHA = "banana123"
USER = f"teste_{random.randint(1000, 9999)}"
random.seed(7)


def post(path, body):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode(), headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


def normalize_key_name(key):
    if key == ".":
        return "period"
    if key == "5":
        return "five"
    if key == "Enter":
        return "Return"
    if len(key) == 1 and "A" <= key <= "Z":
        return f"Shift.{key.lower()}"
    return key


def amostra(rep, texto, escala=1.0, jitter=8.0):
    t, eventos = 1000.0, []
    pares = []
    for i, ch in enumerate(texto):
        hold = (90 + (i % 3) * 15) * escala + random.uniform(-jitter, jitter)
        press_time = round(t, 2)
        release_time = round(t + hold, 2)
        eventos.append({"key": ch, "code": f"Key{ch.upper()}", "timestamp": press_time, "eventType": "KEY_DOWN"})
        eventos.append({"key": ch, "code": f"Key{ch.upper()}", "timestamp": release_time, "eventType": "KEY_UP"})
        pares.append((ch, press_time, release_time))
        t += (160 + (i % 4) * 25) * escala + random.uniform(-jitter, jitter)

    features, counts = {}, {}
    for i in range(len(pares) - 1):
        k1 = normalize_key_name(pares[i][0])
        k2 = normalize_key_name(pares[i + 1][0])
        h_cnt = counts.get(f"H.{k1}", 0)
        counts[f"H.{k1}"] = h_cnt + 1
        features[f"H.{k1}.{h_cnt}"] = round((pares[i][2] - pares[i][1]) / 1000.0, 4)

        dd_cnt = counts.get(f"DD.{k1}.{k2}", 0)
        counts[f"DD.{k1}.{k2}"] = dd_cnt + 1
        features[f"DD.{k1}.{k2}.{dd_cnt}"] = round((pares[i + 1][1] - pares[i][1]) / 1000.0, 4)

        ud_cnt = counts.get(f"UD.{k1}.{k2}", 0)
        counts[f"UD.{k1}.{k2}"] = ud_cnt + 1
        features[f"UD.{k1}.{k2}.{ud_cnt}"] = round((pares[i + 1][1] - pares[i][2]) / 1000.0, 4)

    last_key = normalize_key_name(pares[-1][0])
    h_cnt = counts.get(f"H.{last_key}", 0)
    features[f"H.{last_key}.{h_cnt}"] = round((pares[-1][2] - pares[-1][1]) / 1000.0, 4)

    return {"repetition": rep, "features": features, "rawTimings": eventos, "textEntered": texto,
            "totalDurationMs": round(eventos[-1]["timestamp"] - eventos[0]["timestamp"], 2)}


def check(nome, cond, info):
    print(("OK  " if cond else "FAIL") + f" {nome}: {info}")
    return bool(cond)


resultados = []
s, r = post("/register", {"username": USER, "targetPassword": SENHA, "confirmPassword": SENHA})
resultados.append(check("register", s == 201 and r["success"], (s, r)))
s, r = post("/register", {"username": USER.upper(), "targetPassword": SENHA, "confirmPassword": SENHA})
resultados.append(check("register duplicado (case-insensitive)", s == 409, (s, r)))
s, r = post("/register", {"username": "x_" + USER, "targetPassword": SENHA, "confirmPassword": "outra"})
resultados.append(check("register confirmacao divergente", s == 400, (s, r)))

s, r = post("/authenticate", {"username": USER, "passwordAttempt": SENHA, "sample": amostra(1, SENHA)})
resultados.append(check("login antes da coleta", s == 200 and r["decision"] == "INVALID_CREDENTIALS", (s, r["message"])))

s, r = post("/enrollment", {"username": USER, "samples": [amostra(i, "errada123") for i in range(1, 6)]})
resultados.append(check("enrollment com senha errada", s == 401, (s, r)))
s, r = post("/enrollment", {"username": USER, "samples": [amostra(i, SENHA) for i in range(1, 5)]})
resultados.append(check("enrollment com 4 amostras", s == 400, (s, r)))
s, r = post("/enrollment", {"username": USER, "samples": [amostra(i, SENHA) for i in range(1, 6)]})
resultados.append(check("enrollment", s == 200 and r["samplesRegistered"] == 5, (s, r)))
s, r = post("/enrollment", {"username": USER, "samples": [amostra(i, SENHA) for i in range(1, 6)]})
resultados.append(check("enrollment repetido", s == 409, (s, r)))

s, r = post("/authenticate", {"username": USER, "passwordAttempt": SENHA, "sample": amostra(1, SENHA)})
resultados.append(check("login genuino", s == 200 and r["decision"] == "GENUINE" and r["accessToken"], {k: v for k, v in r.items() if k != "accessToken"}))
s, r = post("/authenticate", {"username": USER, "passwordAttempt": SENHA, "sample": amostra(1, SENHA, escala=2.2)})
resultados.append(check("login impostor (ritmo lento)", s == 200 and r["decision"] == "IMPOSTOR" and not r["accessToken"], r))
s, r = post("/authenticate", {"username": USER, "passwordAttempt": "errada123", "sample": amostra(1, "errada123")})
resultados.append(check("login senha errada", s == 200 and r["decision"] == "INVALID_CREDENTIALS", r["message"]))
s, r = post("/authenticate", {"username": "nao_existe_" + USER, "passwordAttempt": SENHA, "sample": amostra(1, SENHA)})
resultados.append(check("login usuario inexistente", s == 200 and r["message"] == "Usuário ou senha incorretos.", r["message"]))

for _ in range(3):
    post("/authenticate", {"username": USER, "passwordAttempt": "errada123", "sample": amostra(1, "errada123")})
s, r = post("/authenticate", {"username": USER, "passwordAttempt": SENHA, "sample": amostra(1, SENHA)})
resultados.append(check("bloqueio apos 5 falhas", r["decision"] == "INVALID_CREDENTIALS" and "bloqueada" in r["message"], r["message"]))

print(f"\n{sum(resultados)}/{len(resultados)} cenarios ok  (usuario {USER})")
sys.exit(0 if all(resultados) else 1)
