import { Component } from '@angular/core';

@Component({
  selector: 'app-unauthorized',
  standalone: true,
  template: `<section><h2>Unauthorized</h2><p>You do not have permission for this route.</p></section>`,
})
export class UnauthorizedComponent {}
