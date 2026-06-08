shader_type canvas_item;

uniform sampler2D palette_from : source_color, filter_nearest;
uniform sampler2D palette_to   : source_color, filter_nearest;
uniform float     threshold    : hint_range(0.0, 0.15) = 0.01;

void fragment() {
    vec4 original = texture(TEXTURE, UV);
	
    ivec2 pal_size = textureSize(palette_from, 0);
    int   total    = pal_size.x * pal_size.y;

    COLOR = original;

    for (int i = 0; i < total; i++) {
        int  px  = i % pal_size.x;
        int  py  = i / pal_size.x;
        vec2 uv  = (vec2(float(px), float(py)) + 0.5)
                   / vec2(pal_size);

        vec4 from_col = texture(palette_from, uv);
        vec3 diff     = abs(original.rgb - from_col.rgb);

        if (max(diff.r, max(diff.g, diff.b)) <= threshold) {
            vec4 to_col = texture(palette_to, uv);
            COLOR = vec4(to_col.rgb, original.a);
            break;
        }
    }
}