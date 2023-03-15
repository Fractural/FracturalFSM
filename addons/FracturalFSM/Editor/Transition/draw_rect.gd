extends MarginContainer
tool

# Declare member variables here. Examples:
# var a = 2
# var b = "text"


# Called when the node enters the scene tree for the first time.
func _process(delta):
	update()

func _draw():
	var color = Color.white
	color.a = 0.1
	var rect = get_rect()
	rect.position = Vector2.ZERO
	draw_rect(rect, color)
