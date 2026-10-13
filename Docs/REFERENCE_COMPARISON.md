# Referencia visual vs. proyecto actual

Referencia: `Docs/Reference/concept_reference.webp` (1672 × 941, 9 paneles). Recortes ampliados en `Docs/Reference/panel*.png`.
Estado actual comparado: capturas QA de `Genesis_Outpost` del 2026-10-12 (`Logs/qa/`).

## 1. Qué muestra la referencia

### Panel 2 · Zona residencial (objetivo de la primera entrega)
- **Composición:** vista FPS a la altura de los ojos por una calle recta que se pierde hacia el sol, a contraluz y ligeramente a la derecha del centro. Las casas flanquean la calle por ambos lados; la profundidad se lee por la repetición de casas, postes y cables.
- **Calle:** asfalto gris envejecido, agrietado y con parches, con línea central blanca discontinua y borrosa. Los bordes están rotos y sin bordillo marcado; a la derecha hay una acera estrecha de hormigón. Hay escombros, papeles y ramas por el suelo.
- **Casas:** de madera, de dos plantas, con revestimiento de tablas horizontales (lap siding) en gris, beige y marrón desaturado. Tienen:
  - tejados a dos aguas de tejas asfálticas oscuras, con aleros y fascias blancas;
  - chimeneas de ladrillo;
  - porches cubiertos con barandilla y escalones;
  - ventanas de guillotina con marco blanco y cristal (algunas rotas);
  - buhardillas y tejados secundarios.
  
  Están separadas entre 10 y 15 m, con jardines delanteros de hierba alta y seca.
- **Vehículos:** un sedán quemado y oxidado aparcado en el arcén izquierdo, con las ventanillas rotas y los neumáticos deshinchados.
- **Vegetación:** árboles de hoja ancha grandes, de copa redonda y verde oscuro, entre las casas. También hierba alta amarillenta y arbustos en jardines y cunetas.
- **Infraestructura:** postes de madera con travesaños y aisladores. Muchos cables combados cruzan el cielo en diagonal; es un elemento muy característico del encuadre.
- **Iluminación:** atardecer dorado con el sol bajo (unos 5–10°) en el fondo. El cielo va de amarillo y naranja junto al horizonte a azul pizarra con nubes arriba. Hay niebla y bruma cálida en la distancia. Las sombras son largas y azuladas (con contraste frío y cálido), y las fachadas a contraluz quedan con rim light.
- **Arma (EG-47):** rifle negro mate tipo carabina moderna, con estos elementos:
  - mira holográfica rectangular grande;
  - raíl superior y guardamanos largo con ranuras;
  - acentos rojos;
  - empuñadura y cargador curvo;
  - culata esquelética.
  
  Lo sostiene una mano enguantada a la derecha del encuadre, en la parte inferior; ocupa aproximadamente el cuarto inferior derecho.
- **HUD:** minimapa circular arriba a la izquierda, y el resto se ve en los paneles 1 y 8.

### Resto de paneles (para fases siguientes)
| Panel | Elementos clave |
|---|---|
| 1 · Mapa abierto | Pueblo denso de 1–2 plantas con tejados oscuros; dos depósitos de agua metálicos de celosía; cúpula de energía con haz de luz vertical sobre la montaña; cordilleras; primer plano rocoso con hierba seca; árboles dispersos |
| 3 · Base militar / laboratorio | Noche; edificios modulares de hormigón y metal con rótulos "GENESIS"; torres de focos; vallas; vehículos militares |
| 4 · Interiores | Cocina y salón: suelo de madera, sofá, encimeras, armarios altos, ventanas con luz cálida, escombros por el suelo |
| 5 · Aliens | **Stalker:** encorvado, armadura quitinosa negra, venas y ojos rojos brillantes, brazos largos con garras. **Ravager:** gorila blindado con núcleo rojo en el pecho. **Spitter:** delgado, verde, escupe ácido |
| 6 · Armas | EG-47 (rifle de asalto, acentos rojos), VX-9 (subfusil), BR-12 (escopeta en tonos arena), NS-50 (francotirador con visor), PLASMA-X (alienígena morada) |
| 7 · Movimiento | Caminar, correr, apuntar (ADS) y recargar, con brazos y guantes realistas |
| 8 · HUD | Minimapa circular, lista de objetivos, barras de escudo y salud, tarjeta "EG-47 30/120" con icono, indicador de daño, hitmarker |
| 9 · Atmósfera | Nave alienígena estrellada, incendios con humo, helicóptero, depósito de agua, crepúsculo morado |

### Paleta de la referencia
- Cielo junto al horizonte: amarillo y naranja intensos. Arriba: azul y morado pizarra.
- Fachadas: grises, beige y madera envejecida, siempre poco saturados.
- Vegetación: verde oliva oscuro y hierba seca ocre.
- Sombras: azul y gris (no negras ni naranjas).
- Acentos: rojo (arma, aliens) y azul tecnológico (HUD).

## 2. Proyecto actual (Genesis_Outpost) frente a la referencia

| Aspecto | Referencia | Actual | Distancia |
|---|---|---|---|
| Composición | Calle residencial densa con casas a ambos lados | Casas dispersas en parcelas grandes; mucho terreno vacío | **Muy grande** |
| Color global | Contraste cálido y frío, poco saturado | Todo naranja (terreno, cielo, sombras) | **Muy grande** |
| Terreno y suelo | Hierba verde y seca, cunetas, aceras | Suelo naranja uniforme | Grande |
| Calle | Asfalto agrietado con línea central, acera y bordillos | Cajas de asfalto procedural sin aceras | Grande |
| Casas | Variedad: chimeneas, buhardillas, tejas oscuras, cristales, porches | Un solo modelo de Blender sin cristales; teja de barro naranja | Grande |
| Árboles | Hoja ancha de copa redonda | Pinos de tarjetas | Grande |
| Vehículos | Sedán quemado detallado | Cajas procedurales y un camión de cajas | **Muy grande** |
| Postes y cables | Muchos, muy visibles | Hay postes y cables simples | Media |
| Arma | Rifle detallado con mira holográfica | EG-47 de primitivas | **Muy grande** |
| Aliens | Criaturas orgánicas detalladas | Stalker de primitivas | **Muy grande** |
| HUD | Ver panel 8 | Rediseñado con la misma estructura | Pequeña |
| Iluminación | Sol bajo con bruma, cielo con nubes | Sol bajo y niebla, pero sin nubes, con exceso de naranja y cielo negro en vistas altas | Media |

## 3. Diferencias priorizadas (orden de trabajo)

1. **Composición de calle densa** en una escena nueva (`Eclipse_Genesis_OpenWorld_Realistic`), con una zona de 100 × 100 m:
   - calle con asfalto, línea central, aceras y bordillos;
   - casas a ambos lados, a 10–15 m entre sí;
   - jardines con hierba.
2. **Color e iluminación:**
   - quitar el tinte naranja;
   - cielo con nubes;
   - sombras frías;
   - exposición calibrada sin oscurecer.
3. **Variedad y detalle de casas:**
   - tejado de tejas asfálticas oscuras;
   - chimenea de ladrillo;
   - ventanas con cristal y marcos, algunas rotas;
   - buhardilla;
   - variantes de una y dos plantas;
   - edificio comercial de ladrillo.
4. **Postes de madera con muchos cables combados** (muy característico del encuadre).
5. **Árboles de hoja ancha**, hierba alta y arbustos.
6. **Vehículos:** sedán quemado y otro coche.
7. **EG-47 detallado** con mira holográfica, y brazos con guantes.
8. **Stalker orgánico** con rig y animaciones; después Ravager y Spitter.
9. Escombros, decals de suciedad y daños, y barricadas.
10. Fases siguientes: cúpula con haz, depósitos de agua, base nocturna, nave estrellada, fuego, helicóptero.

## 4. Recursos que faltan y opciones

| Recurso | Por qué no basta con lo que hay | Opciones |
|---|---|---|
| Árbol de hoja ancha optimizado | Los árboles de Poly Haven tienen 0,3–4,7 millones de triángulos | (a) Construirlo en Blender con tarjetas usando la textura de hojas de un árbol de Poly Haven (requiere descargarla, CC0). (b) IA 3D |
| Coches (sedán quemado) | Poly Haven solo tiene `covered_car` (tapado con lona) | (a) Modelarlo en Blender (calidad media). (b) IA 3D (Hyper3D Rodin o Hunyuan3D, requiere clave de API). (c) Sketchfab CC-BY (requiere clave de API y atribución) |
| Stalker, Ravager y Spitter | No existen modelos CC0 adecuados | IA 3D para la malla + rig y animación en Blender; o un artista |
| Brazos y guantes FPS | No hay ninguno | IA 3D o modelado en Blender (mano low-poly con rig) |
| Teja asfáltica, ladrillo y cristal sucio | No están descargados | Poly Haven CC0: `roof_tiles_14`/`asphalt_shingles`, `red_brick_03`, texturas de suciedad (requiere autorización de descarga) |
| Hierba alta | `grass_medium_02` tiene 1 millón de triángulos | Tarjetas de hierba hechas en Blender con textura CC0 |
| Barreras de hormigón, boca de incendios, cubos de basura, farolas | Disponibles en Poly Haven (CC0) | Requiere autorización de descarga |

## 5. Primera entrega: calle residencial (2026-10-12)

Escena nueva **`Scenes/Eclipse_Genesis_OpenWorld_Realistic.unity`**, generada con **Eclipse Genesis → Build → Open World Realistic (residential street)** (`ResidentialStreetBuilder`). Las escenas anteriores no se han modificado.

| Elemento pedido | Estado | Origen |
|---|---|---|
| ≥ 4 fachadas detalladas | ✅ 4 variantes de casa (1 y 2 plantas, chimenea, contraventanas, porche, ventanas de guillotina con cristal, algunas rotas, canalones) + tienda de ladrillo con escaparates; 16 edificios en la calle | Blender (`house_builder.py`) + texturas CC0 de Poly Haven |
| Casa explorable | ✅ `SM_House_A` (z = −21, lado izquierdo): 2 plantas, escalera, tabiques, muebles de Poly Haven, luces interiores y reflection probe | Blender + Poly Haven |
| Carretera, aceras, bordillos | ✅ Asfalto agrietado, línea central gastada, aceras de hormigón, bordillos, paso de peatones, accesos, alcantarillas, decals de suciedad y quemaduras | Poly Haven + código |
| Dos vehículos | 🟡 Dos coches tapados con lona (`covered_car`). **Falta el sedán quemado** de la referencia (requiere IA 3D) | Poly Haven |
| Vegetación | ✅ Árboles de hoja ancha (atlas de hojas de Poly Haven), pinos, 9000 matas de hierba alta (verde y seca) | Blender + código |
| Barricadas | ✅ Control militar: barreras de hormigón, sacos terreros, vallas, cartel | Poly Haven + kit |
| Postes y cables | ✅ Dos líneas de postes de madera, transformadores, cables combados, acometidas que cruzan la calle y un cable caído | Código |
| Iluminación HDRP | ✅ Cielo físico con nubes, sol bajo a 8°, niebla volumétrica, SSAO, SSR, sombras de contacto, reflection probes, exposición calibrada y gradación neutra | Volume profile |
| Cámara FPS, EG-47, objetivo | ✅ Jugador `EG_Player`, EG-47 nuevo (`VM_EG47_Detailed`), objetivo "Dirígete a la zona residencial" con marcador en el radar | — |

### Comparación visual (capturas FPS reales de Unity con HUD)
`Docs/Reference/result_street_fps_v1.png` frente a `panel2_residential.png`:

| Aspecto | Resultado |
|---|---|
| Composición (calle hacia el sol, casas a ambos lados, postes y cables cruzando el cielo, árboles entre casas) | Se parece |
| Arquitectura (casas de madera de 1–2 plantas con porche, chimenea de ladrillo, tejado oscuro) | Se parece |
| Arma abajo a la derecha con mira holográfica | Se parece en posición y tipo; el modelo es más simple que el de la referencia |
| Atmósfera | Atardecer dorado, más luminoso y menos dramático que la referencia (menos naranja y menos contraste) |
| Diferencias visibles que quedan | Sin sedán quemado ni escombros en primer plano; brazos de primitivas (no hay guantes realistas); casas limpias, poco envejecidas; colinas del fondo con aspecto de dunas; barrio menos denso visto desde arriba |

**Iteraciones de corrección** (cada una verificada con capturas):
1. Tinte sepia y escena oscura. Causa: la exposición automática a contraluz infraexpone, y además las capturas tomadas justo después de construir la escena salían antes de que el cielo físico convergiera. Solución: compensación de +1,3 EV y calentamiento del cielo antes de capturar.
2. Asfalto marrón: `worn_asphalt` sustituido por `asphalt_02`, gris neutro.
3. Yeso interior rojo (`peeling_painted_wall`): sustituido por `white_plaster_02`.
4. Lista de objetivos ilegible: se muestran 4 objetivos compactos y el panel es más opaco.

### Verificación técnica
- Compilación sin errores.
- EditMode: 26/26.
- PlayMode: tests de combate al completo, incluidos ADS y "rifle visible" con el EG-47 nuevo. El test de salto falla de forma intermitente en la suite completa y pasa al ejecutarlo solo.
- Colisiones comprobadas con raycasts: calle, acera, plantas y escalera de la casa. Los 16 cristales no tienen colisionador y un disparo atraviesa la ventana.
- NavMesh horneado, cubre la calle y el interior.
- Escena: 868 mallas, unos 1,24 millones de triángulos y 13 luces.
- **Rendimiento en juego todavía sin medir en esta escena.** Como referencia, `Genesis_Outpost` dio unos 80 FPS a 2560×1440 en tu equipo.
